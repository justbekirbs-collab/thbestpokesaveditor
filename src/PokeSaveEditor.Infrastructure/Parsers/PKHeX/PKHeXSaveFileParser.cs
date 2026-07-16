using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PKHeX.Core;
using PokeSaveEditor.Core.Enums;
using PokeSaveEditor.Core.Interfaces;
using PokeSaveEditor.Core.Models;
using SaveFileMetadata = PokeSaveEditor.Core.Models.SaveFileMetadata;
using Move = PokeSaveEditor.Core.Models.Move;
using Ability = PokeSaveEditor.Core.Enums.Ability;

namespace PokeSaveEditor.Infrastructure.Parsers.PKHeX;

/// <summary>
/// A universal save file parser implementation powered by PKHeX.Core.
/// Supports all game generations and automatically handles encryption, checksums, and ROM hacks.
/// </summary>
public sealed class PKHeXSaveFileParser : ISaveFileParser
{
    private byte[]? _lastData;
    private SaveFile? _lastSav;

    public GameGeneration Generation => GameGeneration.Custom;

    public bool CanParse(ReadOnlySpan<byte> fileHeader)
    {
        // Catch-all since PKHeX is our universal parser
        return true;
    }

    private SaveFile GetSaveFile(byte[] data)
    {
        if (_lastData != null && _lastData.SequenceEqual(data) && _lastSav != null)
        {
            return _lastSav;
        }

        var sav = RomHackSaveDetector.TryDetect(data);
        if (sav == null)
            throw new InvalidDataException("Invalid or unsupported save file format.");

        _lastData = data;
        _lastSav = sav;
        return sav;
    }

    private void InvalidateCache()
    {
        _lastData = null;
        _lastSav = null;
    }

    public SaveFileMetadata ParseMetadata(IByteReader reader, string filePath)
    {
        byte[] data = reader.ToArray();
        SaveFile sav = GetSaveFile(data);
        GameGeneration gen = MapGameGeneration(sav.Version);

        return new SaveFileMetadata
        {
            Generation = gen,
            TrainerName = sav.OT,
            TrainerId = sav.TID16,
            Badges = (byte)GetBadges(sav),
            PlayTime = new TimeSpan(sav.PlayedHours, sav.PlayedMinutes, sav.PlayedSeconds),
            FileSizeBytes = data.Length,
            FilePath = filePath
        };
    }

    public IReadOnlyList<Pokemon> ParseParty(IByteReader reader)
    {
        byte[] data = reader.ToArray();
        SaveFile sav = GetSaveFile(data);

        var partyList = new List<Pokemon>();
        for (int i = 0; i < sav.PartyCount; i++)
        {
            var pkm = sav.PartyData[i];
            partyList.Add(MapFromPKM(pkm));
        }

        return partyList;
    }

    public IReadOnlyList<Pokemon> ParsePcBoxes(IByteReader reader)
    {
        byte[] data = reader.ToArray();
        SaveFile sav = GetSaveFile(data);

        var pcList = new List<Pokemon>();
        foreach (var pkm in sav.BoxData)
        {
            pcList.Add(MapFromPKM(pkm));
        }

        return pcList;
    }

    public void WriteMetadata(IByteWriter writer, SaveFileMetadata metadata)
    {
        byte[] data = writer.ReadBytes(0, (int)writer.Length).ToArray();
        SaveFile sav = GetSaveFile(data);

        sav.OT = metadata.TrainerName;
        sav.TID16 = metadata.TrainerId;
        SetBadges(sav, metadata.Badges);

        byte[] output = sav.Write();
        writer.WriteBytes(0, output);
        InvalidateCache();
    }

    public void WriteParty(IByteWriter writer, IReadOnlyList<Pokemon> party)
    {
        byte[] data = writer.ReadBytes(0, (int)writer.Length).ToArray();
        SaveFile sav = GetSaveFile(data);

        byte[] rawData = GetRawData(sav);
        if (rawData == null)
            throw new InvalidOperationException("Could not retrieve raw save data.");

        for (int i = 0; i < party.Count; i++)
        {
            var pkm = sav.BlankPKM;
            MapToPKM(party[i], pkm);
            int offset = sav.GetPartyOffset(i);
            sav.WritePartySlot(pkm, rawData.AsSpan(offset));
        }
        SetPartyCount(sav, party.Count);

        byte[] output = sav.Write();
        writer.WriteBytes(0, output);
        InvalidateCache();
    }

    public void WritePcBoxes(IByteWriter writer, IReadOnlyList<Pokemon> pcBoxes)
    {
        byte[] data = writer.ReadBytes(0, (int)writer.Length).ToArray();
        SaveFile sav = GetSaveFile(data);

        byte[] rawData = GetRawData(sav);
        if (rawData == null)
            throw new InvalidOperationException("Could not retrieve raw save data.");

        for (int i = 0; i < pcBoxes.Count; i++)
        {
            var pkm = sav.BlankPKM;
            MapToPKM(pcBoxes[i], pkm);
            int offset = sav.GetBoxSlotOffset(i);
            sav.WriteBoxSlot(pkm, rawData.AsSpan(offset));
        }

        byte[] output = sav.Write();
        writer.WriteBytes(0, output);
        InvalidateCache();
    }

    // --- Helper Mapping Methods ---

    public static Pokemon MapFromPKM(PKM pkm)
    {
        EnsurePK3Unshuffled(pkm);
        var moves = new Move[4];
        moves[0] = new Move(pkm.Move1, (byte)pkm.Move1_PP, (byte)pkm.Move1_PPUps);
        moves[1] = new Move(pkm.Move2, (byte)pkm.Move2_PP, (byte)pkm.Move2_PPUps);
        moves[2] = new Move(pkm.Move3, (byte)pkm.Move3_PP, (byte)pkm.Move3_PPUps);
        moves[3] = new Move(pkm.Move4, (byte)pkm.Move4_PP, (byte)pkm.Move4_PPUps);

        ushort speciesId = pkm.Species;
        if (speciesId == 0 && pkm is PK3 pk3)
        {
            speciesId = pk3.SpeciesInternal;
        }

        return new Pokemon
        {
            PersonalityValue = pkm.PID,
            Species = (PokemonSpecies)speciesId,
            Nickname = pkm.Nickname,
            Level = pkm.CurrentLevel,
            Experience = pkm.EXP,
            OriginalTrainer = new TrainerInfo
            {
                Name = pkm.OriginalTrainerName,
                PublicId = pkm.TID16,
                SecretId = pkm.SID16
            },
            IVs = new StatBlock
            {
                Hp = (byte)pkm.IV_HP,
                Attack = (byte)pkm.IV_ATK,
                Defense = (byte)pkm.IV_DEF,
                Speed = (byte)pkm.IV_SPE,
                SpAttack = (byte)pkm.IV_SPA,
                SpDefense = (byte)pkm.IV_SPD
            },
            EVs = new StatBlock
            {
                Hp = (byte)pkm.EV_HP,
                Attack = (byte)pkm.EV_ATK,
                Defense = (byte)pkm.EV_DEF,
                Speed = (byte)pkm.EV_SPE,
                SpAttack = (byte)pkm.EV_SPA,
                SpDefense = (byte)pkm.EV_SPD
            },
            Ability = (Ability)pkm.Ability,
            HeldItem = (ushort)pkm.HeldItem,
            Moves = moves,
            IsEgg = pkm.IsEgg,
            Friendship = pkm.CurrentFriendship,
            PokerusStatus = (byte)(pkm.PokerusDays | (pkm.PokerusStrain << 4)),
            MetLocation = pkm.MetLocation,
            MetLevel = pkm.MetLevel,
            BallType = pkm.Ball
        };
    }

    public static void MapToPKM(Pokemon src, PKM dest)
    {
        ushort targetSpecies = (ushort)src.Species;
        dest.Species = targetSpecies;
        if (dest.Species != targetSpecies)
        {
            var prop = dest.GetType().GetProperty("SpeciesInternal");
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(dest, targetSpecies);
            }
        }
        dest.Nickname = src.Nickname;
        dest.CurrentLevel = src.Level;
        dest.EXP = src.Experience;
        dest.PID = src.PersonalityValue;

        dest.OriginalTrainerName = src.OriginalTrainer.Name;
        dest.TID16 = src.OriginalTrainer.PublicId;
        dest.SID16 = src.OriginalTrainer.SecretId;

        dest.IV_HP = src.IVs.Hp;
        dest.IV_ATK = src.IVs.Attack;
        dest.IV_DEF = src.IVs.Defense;
        dest.IV_SPE = src.IVs.Speed;
        dest.IV_SPA = src.IVs.SpAttack;
        dest.IV_SPD = src.IVs.SpDefense;

        dest.EV_HP = src.EVs.Hp;
        dest.EV_ATK = src.EVs.Attack;
        dest.EV_DEF = src.EVs.Defense;
        dest.EV_SPE = src.EVs.Speed;
        dest.EV_SPA = src.EVs.SpAttack;
        dest.EV_SPD = src.EVs.SpDefense;

        dest.Ability = (int)src.Ability;
        dest.HeldItem = src.HeldItem;

        dest.Move1 = src.Moves[0].MoveId;
        dest.Move1_PP = src.Moves[0].CurrentPp;
        dest.Move1_PPUps = src.Moves[0].PpUps;

        dest.Move2 = src.Moves[1].MoveId;
        dest.Move2_PP = src.Moves[1].CurrentPp;
        dest.Move2_PPUps = src.Moves[1].PpUps;

        dest.Move3 = src.Moves[2].MoveId;
        dest.Move3_PP = src.Moves[2].CurrentPp;
        dest.Move3_PPUps = src.Moves[2].PpUps;

        dest.Move4 = src.Moves[3].MoveId;
        dest.Move4_PP = src.Moves[3].CurrentPp;
        dest.Move4_PPUps = src.Moves[3].PpUps;

        dest.IsEgg = src.IsEgg;
        dest.CurrentFriendship = src.Friendship;
        
        dest.PokerusDays = src.PokerusStatus & 0xF;
        dest.PokerusStrain = src.PokerusStatus >> 4;

        dest.MetLocation = src.MetLocation;
        dest.MetLevel = src.MetLevel;
        dest.Ball = src.BallType;
    }

    private static byte[] GetRawData(SaveFile sav)
    {
        var type = sav.GetType();
        while (type != null)
        {
            var field = type.GetField("Data", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                var val = field.GetValue(sav);
                if (val is byte[] arr)
                    return arr;
            }
            type = type.BaseType;
        }
        return null;
    }

    private static int GetBadges(SaveFile sav)
    {
        if (sav is SAV3 sav3)
        {
            bool isFRLG = sav3 is SAV3FRLG;
            if (isFRLG)
            {
                return sav3.Large[3968 + 884];
            }
            else
            {
                byte b1 = (byte)((sav3.Large[3968 + 1008] & 0x80) >> 7);
                byte b2_8 = (byte)((sav3.Large[3968 + 1009] & 0x7F) << 1);
                return (byte)(b1 | b2_8);
            }
        }

        var prop = sav.GetType().GetProperty("Badges");
        if (prop != null && prop.CanRead)
        {
            return Convert.ToInt32(prop.GetValue(sav));
        }
        return 0;
    }

    private static void SetBadges(SaveFile sav, int badges)
    {
        if (sav is SAV3 sav3)
        {
            bool isFRLG = sav3 is SAV3FRLG;
            if (isFRLG)
            {
                sav3.Large[3968 + 884] = (byte)badges;
            }
            else
            {
                sav3.Large[3968 + 1008] = (byte)((sav3.Large[3968 + 1008] & 0x7F) | ((badges & 1) << 7));
                sav3.Large[3968 + 1009] = (byte)((sav3.Large[3968 + 1009] & 0x80) | ((badges >> 1) & 0x7F));
            }
            return;
        }

        var prop = sav.GetType().GetProperty("Badges");
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(sav, Convert.ChangeType(badges, prop.PropertyType));
        }
    }

    private static void SetPartyCount(SaveFile sav, int count)
    {
        var prop = sav.GetType().GetProperty("PartyCount");
        if (prop != null)
        {
            prop.SetValue(sav, count);
        }
    }

    private static GameGeneration MapGameGeneration(GameVersion version)
    {
        return version switch
        {
            GameVersion.RD or GameVersion.BU or GameVersion.YW or GameVersion.GP => GameGeneration.Gen1_RedBlueYellow,
            GameVersion.GD or GameVersion.SI or GameVersion.C => GameGeneration.Gen2_GoldSilverCrystal,
            GameVersion.R or GameVersion.S => GameGeneration.Gen3_RubySapphire,
            GameVersion.E => GameGeneration.Gen3_Emerald,
            GameVersion.FR or GameVersion.LG or GameVersion.FRLG => GameGeneration.Gen3_FireRedLeafGreen,
            GameVersion.D or GameVersion.P or GameVersion.DP => GameGeneration.Gen4_DiamondPearl,
            GameVersion.Pt => GameGeneration.Gen4_Platinum,
            GameVersion.HG or GameVersion.SS or GameVersion.HGSS => GameGeneration.Gen4_HeartGoldSoulSilver,
            GameVersion.W or GameVersion.B or GameVersion.BW => GameGeneration.Gen5_BlackWhite,
            GameVersion.W2 or GameVersion.B2 or GameVersion.B2W2 => GameGeneration.Gen5_Black2White2,
            GameVersion.X or GameVersion.Y or GameVersion.XY => GameGeneration.Gen6_XY,
            GameVersion.OR or GameVersion.AS or GameVersion.ORAS => GameGeneration.Gen6_ORAS,
            GameVersion.SN or GameVersion.MN or GameVersion.SM => GameGeneration.Gen7_SunMoon,
            GameVersion.US or GameVersion.UM or GameVersion.USUM => GameGeneration.Gen7_USUM,
            GameVersion.SW or GameVersion.SH or GameVersion.SWSH => GameGeneration.Gen8_SwordShield,
            GameVersion.BD or GameVersion.SP or GameVersion.BDSP => GameGeneration.Gen8_BDSP,
            GameVersion.PLA => GameGeneration.Gen8_LegendsArceus,
            GameVersion.SL or GameVersion.VL or GameVersion.SV => GameGeneration.Gen9_ScarletViolet,
            _ => GameGeneration.Custom
        };
    }

    private static void EnsurePK3Unshuffled(PKM pkm)
    {
        if (pkm is not PK3 pk3)
            return;

        var data = pk3.Data;
        if (data.Length < 80)
            return;

        ushort storedChecksum = BitConverter.ToUInt16(data, 0x1C);
        
        ushort sum = 0;
        for (int i = 0x20; i < 0x50; i += 2)
        {
            sum += BitConverter.ToUInt16(data, i);
        }

        uint pv = pk3.PID;
        int orderIndex = (int)(pv % 24);

        if (sum != storedChecksum && storedChecksum != 0)
        {
            return;
        }

        if (orderIndex == 0)
        {
            return;
        }

        // The blocks are decrypted but still shuffled. We must unshuffle them.
        int[][] shuffleOrders = [
            [0, 1, 2, 3], [0, 1, 3, 2], [0, 2, 1, 3], [0, 2, 3, 1], [0, 3, 1, 2], [0, 3, 2, 1],
            [1, 0, 2, 3], [1, 0, 3, 2], [1, 2, 0, 3], [1, 2, 3, 0], [1, 3, 0, 2], [1, 3, 2, 0],
            [2, 0, 1, 3], [2, 0, 3, 1], [2, 1, 0, 3], [2, 1, 3, 0], [2, 3, 0, 1], [2, 3, 1, 0],
            [3, 0, 1, 2], [3, 0, 2, 1], [3, 1, 0, 2], [3, 1, 2, 0], [3, 2, 0, 1], [3, 2, 1, 0]
        ];

        int[] order = shuffleOrders[orderIndex];
        byte[] temp = new byte[48];
        Array.Copy(data, 0x20, temp, 0, 48);

        const int blockSize = 12;
        for (int i = 0; i < 4; i++)
        {
            int sourcePosition = i * blockSize;
            int destPosition = order[i] * blockSize;
            Array.Copy(temp, sourcePosition, data, 0x20 + destPosition, blockSize);
        }
    }
}
