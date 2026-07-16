namespace PokeSaveEditor.Infrastructure.Parsers.Gen3;

using System.Buffers.Binary;
using PokeSaveEditor.Core.Enums;
using PokeSaveEditor.Core.Interfaces;
using PokeSaveEditor.Core.Models;
using PokeSaveEditor.Infrastructure.Extensions;

/// <summary>
/// Save file parser for Generation III games (Ruby/Sapphire/Emerald/FireRed/LeafGreen).
/// Implements the Strategy pattern via <see cref="ISaveFileParser"/>.
/// </summary>
public sealed class Gen3SaveFileParser : ISaveFileParser
{
    private readonly Gen3ChecksumCalculator _checksumCalc = new();

    /// <inheritdoc />
    public GameGeneration Generation => GameGeneration.Gen3_RubySapphire;

    /// <inheritdoc />
    public bool CanParse(ReadOnlySpan<byte> fileHeader)
    {
        // Search for standard GBA section signature 0x08012025 (little-endian)
        if (fileHeader.Length >= 0xFFC && BinaryPrimitives.ReadUInt32LittleEndian(fileHeader[0xFF8..]) == 0x08012025)
            return true;
        if (fileHeader.Length >= 512 + 0xFFC && BinaryPrimitives.ReadUInt32LittleEndian(fileHeader[(512 + 0xFF8)..]) == 0x08012025)
            return true;
        return false;
    }

    /// <inheritdoc />
    public SaveFileMetadata ParseMetadata(IByteReader reader, string filePath)
    {
        int headerShift = GetHeaderShift(reader);
        int slotOffset = FindActiveSlotOffset(reader, headerShift);
        int trainerSectionOffset = FindSectionOffset(reader, headerShift, slotOffset, Gen3OffsetMap.TrainerSection);

        ReadOnlySpan<byte> section = reader.ReadBytes(trainerSectionOffset, Gen3OffsetMap.SectionSize);

        string trainerName = section.DecodeGen3String(
            Gen3OffsetMap.TrainerNameOffset,
            Gen3OffsetMap.TrainerNameLength);

        uint trainerId = section.ReadUInt32LE(Gen3OffsetMap.TrainerIdOffset);
        ushort publicId = (ushort)(trainerId & 0xFFFF);

        // Parse play time
        ushort hours = section.ReadUInt16LE(Gen3OffsetMap.PlayTimeOffset);
        byte minutes = section[Gen3OffsetMap.PlayTimeOffset + 2];
        byte seconds = section[Gen3OffsetMap.PlayTimeOffset + 3];

        // Detect generation (FRLG vs Ruby/Sapphire/Emerald)
        int partySectionOffset = FindSectionOffset(reader, headerShift, slotOffset, Gen3OffsetMap.PartySectionId);
        ReadOnlySpan<byte> partySection = reader.ReadBytes(partySectionOffset, Gen3OffsetMap.SectionSize);
        bool isFRLG = IsFireRedLeafGreen(partySection);

        // Read trainer badges from Game State section (Section 2)
        int gameStateSectionOffset = FindSectionOffset(reader, headerShift, slotOffset, 2);
        ReadOnlySpan<byte> gameStateSection = reader.ReadBytes(gameStateSectionOffset, Gen3OffsetMap.SectionSize);
        byte badges = ReadBadges(gameStateSection, isFRLG);

        return new SaveFileMetadata
        {
            Generation = isFRLG ? GameGeneration.Gen3_FireRedLeafGreen : GameGeneration.Gen3_RubySapphire,
            TrainerName = trainerName,
            TrainerId = publicId,
            Badges = badges,
            PlayTime = new TimeSpan(hours, minutes, seconds),
            FileSizeBytes = reader.Length,
            FilePath = filePath
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<Pokemon> ParseParty(IByteReader reader)
    {
        int headerShift = GetHeaderShift(reader);
        int slotOffset = FindActiveSlotOffset(reader, headerShift);
        int partySectionOffset = FindSectionOffset(reader, headerShift, slotOffset, Gen3OffsetMap.PartySectionId);

        ReadOnlySpan<byte> section = reader.ReadBytes(partySectionOffset, Gen3OffsetMap.SectionSize);

        // Dynamically detect game variant (FRLG vs. RS/E/Emerald)
        bool isFRLG = IsFireRedLeafGreen(section);
        int partyCountOffset = isFRLG ? Gen3OffsetMap.PartyCountOffset_FRLG : Gen3OffsetMap.PartyCountOffset_RS;
        int partyDataOffset = isFRLG ? Gen3OffsetMap.PartyDataOffset_FRLG : Gen3OffsetMap.PartyDataOffset_RS;

        int partyCount = Math.Min(
            (int)section.ReadUInt32LE(partyCountOffset),
            Gen3OffsetMap.MaxPartySize);

        var party = new List<Pokemon>(partyCount);

        for (int i = 0; i < partyCount; i++)
        {
            int pokemonOffset = partySectionOffset + partyDataOffset
                                + (i * Gen3OffsetMap.PartyPokemonSize);

            ReadOnlySpan<byte> pokemonData = reader.ReadBytes(pokemonOffset, Gen3OffsetMap.PartyPokemonSize);
            Pokemon? pokemon = ParseSinglePokemon(pokemonData);

            if (pokemon is not null)
                party.Add(pokemon);
        }

        return party;
    }

    /// <inheritdoc />
    public IReadOnlyList<Pokemon> ParsePcBoxes(IByteReader reader)
    {
        int headerShift = GetHeaderShift(reader);
        int slotOffset = FindActiveSlotOffset(reader, headerShift);

        // Concatenate sections 5 to 13 (PC buffer sections)
        byte[] pcBuffer = new byte[9 * 4084];

        for (int i = 0; i < 9; i++)
        {
            int sectionId = 5 + i;
            try
            {
                int sectionOffset = FindSectionOffset(reader, headerShift, slotOffset, sectionId);
                ReadOnlySpan<byte> sectionData = reader.ReadBytes(sectionOffset, 4084);
                sectionData.CopyTo(pcBuffer.AsSpan(i * 4084, 4084));
            }
            catch (Exception)
            {
                // If any PC box section is missing or corrupt, return empty list
                return [];
            }
        }

        var pcList = new List<Pokemon>();
        // PC Pokémon storage starts at offset 4 of the reconstructed PC buffer
        // Size: 14 boxes * 30 slots = 420 slots
        const int pokemonDataStart = 4;
        const int totalSlots = Gen3OffsetMap.PcBoxCount * Gen3OffsetMap.PcBoxSlotCount;

        for (int i = 0; i < totalSlots; i++)
        {
            int offset = pokemonDataStart + (i * Gen3OffsetMap.PcPokemonSize);
            ReadOnlySpan<byte> pokemonData = pcBuffer.AsSpan(offset, Gen3OffsetMap.PcPokemonSize);

            Pokemon? pokemon = ParseSinglePokemon(pokemonData);
            if (pokemon is not null)
            {
                // GBA saves don't store Level in the PC box, so we estimate it from Experience
                if (pokemon.Level == 0)
                {
                    double exp = pokemon.Experience;
                    int estimatedLevel = (int)Math.Round(Math.Pow(exp, 1.0 / 3.0));
                    pokemon.Level = (byte)Math.Clamp(estimatedLevel, 1, 100);
                }

                pcList.Add(pokemon);
            }
        }

        return pcList;
    }

    /// <inheritdoc />
    public void WriteParty(IByteWriter writer, IReadOnlyList<Pokemon> party)
    {
        int headerShift = GetHeaderShiftForWriter(writer);
        int slotOffset = FindActiveSlotOffsetForWriter(writer, headerShift);
        int partySectionOffset = FindSectionOffsetForWriter(writer, headerShift, slotOffset, Gen3OffsetMap.PartySectionId);

        byte[] sectionData = writer.ReadBytes(partySectionOffset, Gen3OffsetMap.SectionSize).ToArray();

        // Dynamically detect game variant (FRLG vs. RS/E/Emerald)
        bool isFRLG = IsFireRedLeafGreen(sectionData);
        int partyCountOffset = isFRLG ? Gen3OffsetMap.PartyCountOffset_FRLG : Gen3OffsetMap.PartyCountOffset_RS;
        int partyDataOffset = isFRLG ? Gen3OffsetMap.PartyDataOffset_FRLG : Gen3OffsetMap.PartyDataOffset_RS;

        int partyCount = Math.Min(party.Count, Gen3OffsetMap.MaxPartySize);
        BinaryPrimitives.WriteUInt32LittleEndian(sectionData.AsSpan(partyCountOffset), (uint)partyCount);

        for (int i = 0; i < Gen3OffsetMap.MaxPartySize; i++)
        {
            int pokemonOffset = partyDataOffset + (i * Gen3OffsetMap.PartyPokemonSize);
            byte[] pkmnBytes = i < partyCount 
                ? SerializePokemon(party[i], isParty: true) 
                : new byte[Gen3OffsetMap.PartyPokemonSize];

            pkmnBytes.CopyTo(sectionData.AsSpan(pokemonOffset, Gen3OffsetMap.PartyPokemonSize));
        }

        ushort checksum = _checksumCalc.Calculate(sectionData.AsSpan(0, 3968));
        BinaryPrimitives.WriteUInt16LittleEndian(sectionData.AsSpan(Gen3OffsetMap.SectionChecksumOffset), checksum);

        writer.WriteBytes(partySectionOffset, sectionData);
    }

    /// <inheritdoc />
    public void WritePcBoxes(IByteWriter writer, IReadOnlyList<Pokemon> pcBoxes)
    {
        int headerShift = GetHeaderShiftForWriter(writer);
        int slotOffset = FindActiveSlotOffsetForWriter(writer, headerShift);

        byte[] pcBuffer = new byte[9 * 4084];
        int[] sectionOffsets = new int[9];

        for (int i = 0; i < 9; i++)
        {
            int sectionId = 5 + i;
            sectionOffsets[i] = FindSectionOffsetForWriter(writer, headerShift, slotOffset, sectionId);
            ReadOnlySpan<byte> sectionData = writer.ReadBytes(sectionOffsets[i], 4084);
            sectionData.CopyTo(pcBuffer.AsSpan(i * 4084, 4084));
        }

        const int pokemonDataStart = 4;
        const int totalSlots = Gen3OffsetMap.PcBoxCount * Gen3OffsetMap.PcBoxSlotCount;

        for (int i = 0; i < totalSlots; i++)
        {
            int offset = pokemonDataStart + (i * Gen3OffsetMap.PcPokemonSize);
            byte[] pkmnBytes = i < pcBoxes.Count 
                ? SerializePokemon(pcBoxes[i], isParty: false) 
                : new byte[Gen3OffsetMap.PcPokemonSize];

            pkmnBytes.CopyTo(pcBuffer.AsSpan(offset, Gen3OffsetMap.PcPokemonSize));
        }

        for (int i = 0; i < 9; i++)
        {
            int sectionOffset = sectionOffsets[i];
            byte[] fullSection = writer.ReadBytes(sectionOffset, Gen3OffsetMap.SectionSize).ToArray();
            pcBuffer.AsSpan(i * 4084, 4084).CopyTo(fullSection.AsSpan(0, 4084));

            ushort checksum = _checksumCalc.Calculate(fullSection.AsSpan(0, 3968));
            BinaryPrimitives.WriteUInt16LittleEndian(fullSection.AsSpan(Gen3OffsetMap.SectionChecksumOffset), checksum);

            writer.WriteBytes(sectionOffset, fullSection);
        }
    }

    /// <inheritdoc />
    public void WriteMetadata(IByteWriter writer, SaveFileMetadata metadata)
    {
        int headerShift = GetHeaderShiftForWriter(writer);
        int slotOffset = FindActiveSlotOffsetForWriter(writer, headerShift);
        
        // 1. Write Trainer Info (Section 0)
        int trainerSectionOffset = FindSectionOffsetForWriter(writer, headerShift, slotOffset, Gen3OffsetMap.TrainerSection);
        byte[] sectionData = writer.ReadBytes(trainerSectionOffset, Gen3OffsetMap.SectionSize).ToArray();

        byte[] nameBytes = EncodeGen3String(metadata.TrainerName, Gen3OffsetMap.TrainerNameLength);
        nameBytes.CopyTo(sectionData.AsSpan(Gen3OffsetMap.TrainerNameOffset, Gen3OffsetMap.TrainerNameLength));

        uint currentFullId = BinaryPrimitives.ReadUInt32LittleEndian(sectionData.AsSpan(Gen3OffsetMap.TrainerIdOffset, 4));
        ushort secretId = (ushort)(currentFullId >> 16);
        uint newFullId = (uint)(secretId << 16) | metadata.TrainerId;
        BinaryPrimitives.WriteUInt32LittleEndian(sectionData.AsSpan(Gen3OffsetMap.TrainerIdOffset), newFullId);

        ushort checksum = _checksumCalc.Calculate(sectionData.AsSpan(0, 3968));
        BinaryPrimitives.WriteUInt16LittleEndian(sectionData.AsSpan(Gen3OffsetMap.SectionChecksumOffset), checksum);

        writer.WriteBytes(trainerSectionOffset, sectionData);

        // 2. Write Badges (Section 2)
        int gameStateSectionOffset = FindSectionOffsetForWriter(writer, headerShift, slotOffset, 2);
        byte[] gameStateSectionData = writer.ReadBytes(gameStateSectionOffset, Gen3OffsetMap.SectionSize).ToArray();

        int partySectionOffset = FindSectionOffsetForWriter(writer, headerShift, slotOffset, Gen3OffsetMap.PartySectionId);
        ReadOnlySpan<byte> partySection = writer.ReadBytes(partySectionOffset, Gen3OffsetMap.SectionSize);
        bool isFRLG = IsFireRedLeafGreen(partySection);

        WriteBadges(gameStateSectionData, metadata.Badges, isFRLG);

        ushort gameStateChecksum = _checksumCalc.Calculate(gameStateSectionData.AsSpan(0, 3968));
        BinaryPrimitives.WriteUInt16LittleEndian(gameStateSectionData.AsSpan(Gen3OffsetMap.SectionChecksumOffset), gameStateChecksum);

        writer.WriteBytes(gameStateSectionOffset, gameStateSectionData);
    }

    // --- Private Write Helpers ---

    private static byte[] SerializePokemon(Pokemon pokemon, bool isParty)
    {
        byte[] data = new byte[isParty ? Gen3OffsetMap.PartyPokemonSize : Gen3OffsetMap.PcPokemonSize];

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(Gen3OffsetMap.PkmnPersonalityOffset), pokemon.PersonalityValue);
        
        uint otId = pokemon.OriginalTrainer.FullId;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(Gen3OffsetMap.PkmnOtIdOffset), otId);

        byte[] nickBytes = EncodeGen3String(pokemon.Nickname, Gen3OffsetMap.PkmnNicknameLength);
        nickBytes.CopyTo(data.AsSpan(Gen3OffsetMap.PkmnNicknameOffset, Gen3OffsetMap.PkmnNicknameLength));

        byte[] otBytes = EncodeGen3String(pokemon.OriginalTrainer.Name, Gen3OffsetMap.TrainerNameLength);
        otBytes.CopyTo(data.AsSpan(Gen3OffsetMap.PkmnOtNameOffset, Gen3OffsetMap.TrainerNameLength));

        data[0x1B] = 2; // English language

        byte[] canonical = new byte[Gen3OffsetMap.PkmnSubstructureSize];

        // Block G (Growth)
        BinaryPrimitives.WriteUInt16LittleEndian(canonical.AsSpan(Gen3OffsetMap.SubG_Species), (ushort)pokemon.Species);
        BinaryPrimitives.WriteUInt16LittleEndian(canonical.AsSpan(Gen3OffsetMap.SubG_HeldItem), pokemon.HeldItem);
        BinaryPrimitives.WriteUInt32LittleEndian(canonical.AsSpan(Gen3OffsetMap.SubG_Experience), pokemon.Experience);
        canonical[Gen3OffsetMap.SubG_Friendship] = pokemon.Friendship;

        // Block A (Attacks)
        const int blockA = Gen3OffsetMap.SubstructureBlockSize;
        BinaryPrimitives.WriteUInt16LittleEndian(canonical.AsSpan(blockA + Gen3OffsetMap.SubA_Move1), pokemon.Moves[0].MoveId);
        BinaryPrimitives.WriteUInt16LittleEndian(canonical.AsSpan(blockA + Gen3OffsetMap.SubA_Move2), pokemon.Moves[1].MoveId);
        BinaryPrimitives.WriteUInt16LittleEndian(canonical.AsSpan(blockA + Gen3OffsetMap.SubA_Move3), pokemon.Moves[2].MoveId);
        BinaryPrimitives.WriteUInt16LittleEndian(canonical.AsSpan(blockA + Gen3OffsetMap.SubA_Move4), pokemon.Moves[3].MoveId);
        canonical[blockA + Gen3OffsetMap.SubA_Pp1] = pokemon.Moves[0].CurrentPp;
        canonical[blockA + Gen3OffsetMap.SubA_Pp2] = pokemon.Moves[1].CurrentPp;
        canonical[blockA + Gen3OffsetMap.SubA_Pp3] = pokemon.Moves[2].CurrentPp;
        canonical[blockA + Gen3OffsetMap.SubA_Pp4] = pokemon.Moves[3].CurrentPp;

        // Block E (EVs)
        const int blockE = Gen3OffsetMap.SubstructureBlockSize * 2;
        canonical[blockE + Gen3OffsetMap.SubE_HpEv] = pokemon.EVs.Hp;
        canonical[blockE + Gen3OffsetMap.SubE_AtkEv] = pokemon.EVs.Attack;
        canonical[blockE + Gen3OffsetMap.SubE_DefEv] = pokemon.EVs.Defense;
        canonical[blockE + Gen3OffsetMap.SubE_SpeEv] = pokemon.EVs.Speed;
        canonical[blockE + Gen3OffsetMap.SubE_SpaEv] = pokemon.EVs.SpAttack;
        canonical[blockE + Gen3OffsetMap.SubE_SpdEv] = pokemon.EVs.SpDefense;

        // Block M (Misc)
        const int blockM = Gen3OffsetMap.SubstructureBlockSize * 3;
        canonical[blockM + Gen3OffsetMap.SubM_PokerusStatus] = pokemon.PokerusStatus;
        canonical[blockM + Gen3OffsetMap.SubM_MetLocation] = (byte)pokemon.MetLocation;
        
        ushort origins = (ushort)(pokemon.MetLevel & 0x7F);
        origins |= (ushort)((pokemon.BallType & 0x0F) << 11);
        BinaryPrimitives.WriteUInt16LittleEndian(canonical.AsSpan(blockM + Gen3OffsetMap.SubM_Origins), origins);

        uint ivData = pokemon.IVs.ToPackedIvs();
        if (pokemon.IsEgg)
            ivData |= (1u << 30);
        uint abilityBit = (uint)((byte)pokemon.Ability & 1);
        ivData |= (abilityBit << 31);
        BinaryPrimitives.WriteUInt32LittleEndian(canonical.AsSpan(blockM + Gen3OffsetMap.SubM_IvsAbility), ivData);

        ushort checksum = Gen3Decryptor.CalculateChecksum(canonical);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(Gen3OffsetMap.PkmnChecksumOffset), checksum);

        byte[] encrypted = Gen3Decryptor.EncryptSubstructure(canonical, pokemon.PersonalityValue, otId);
        encrypted.CopyTo(data.AsSpan(Gen3OffsetMap.PkmnSubstructureOffset, Gen3OffsetMap.PkmnSubstructureSize));

        if (isParty)
        {
            data[84] = pokemon.Level;
            data[80] = 0; // status condition
            ushort maxHp = (ushort)(pokemon.Level * 3 + 15);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(86), maxHp);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(88), maxHp);
            ushort basicStat = (ushort)(pokemon.Level * 2 + 10);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(90), basicStat);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(92), basicStat);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(94), basicStat);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(96), basicStat);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(98), basicStat);
        }

        return data;
    }

    private static byte[] EncodeGen3String(string text, int maxLength)
    {
        byte[] bytes = new byte[maxLength];
        Array.Fill(bytes, (byte)0xFF);

        int len = Math.Min(text.Length, maxLength);
        for (int i = 0; i < len; i++)
        {
            char c = text[i];
            byte b = c switch
            {
                >= 'A' and <= 'Z' => (byte)(0xBB + (c - 'A')),
                >= 'a' and <= 'z' => (byte)(0xD5 + (c - 'a')),
                >= '0' and <= '9' => (byte)(0xA1 + (c - '0')),
                ' ' => 0x00,
                '!' => 0xAB,
                '?' => 0xAC,
                '.' => 0xAD,
                '-' => 0xAE,
                '\u2026' => 0xB0,
                '\u201C' => 0xB1,
                '\u201D' => 0xB2,
                '\u2018' => 0xB3,
                '\u2019' => 0xB4,
                _ => 0x00
            };
            bytes[i] = b;
        }

        return bytes;
    }

    // --- Private helpers ---

    private static int GetHeaderShift(IByteReader reader)
    {
        if (reader.Length > 0xFFC && reader.ReadUInt32(0xFF8) == 0x08012025)
            return 0;
        if (reader.Length > 512 + 0xFFC && reader.ReadUInt32(512 + 0xFF8) == 0x08012025)
            return 512;
        return 0;
    }

    private static int GetHeaderShiftForWriter(IByteWriter writer)
    {
        if (writer.Length > 0xFFC && BinaryPrimitives.ReadUInt32LittleEndian(writer.ReadBytes(0xFF8, 4)) == 0x08012025)
            return 0;
        if (writer.Length > 512 + 0xFFC && BinaryPrimitives.ReadUInt32LittleEndian(writer.ReadBytes(512 + 0xFF8, 4)) == 0x08012025)
            return 512;
        return 0;
    }

    private static int FindActiveSlotOffset(IByteReader reader, int headerShift)
    {
        if (reader.Length < headerShift + Gen3OffsetMap.SaveSlotSize * 2)
            return 0; // Single slot (e.g. 64KB)

        uint indexA = reader.ReadUInt32(headerShift + Gen3OffsetMap.SectionSaveIndexOffset);
        uint indexB = reader.ReadUInt32(headerShift + Gen3OffsetMap.SaveSlotSize + Gen3OffsetMap.SectionSaveIndexOffset);

        return indexB > indexA ? Gen3OffsetMap.SaveSlotSize : 0;
    }

    private static int FindActiveSlotOffsetForWriter(IByteWriter writer, int headerShift)
    {
        if (writer.Length < headerShift + Gen3OffsetMap.SaveSlotSize * 2)
            return 0;

        uint indexA = BinaryPrimitives.ReadUInt32LittleEndian(writer.ReadBytes(headerShift + Gen3OffsetMap.SectionSaveIndexOffset, 4));
        uint indexB = BinaryPrimitives.ReadUInt32LittleEndian(writer.ReadBytes(headerShift + Gen3OffsetMap.SaveSlotSize + Gen3OffsetMap.SectionSaveIndexOffset, 4));
        return indexB > indexA ? Gen3OffsetMap.SaveSlotSize : 0;
    }

    private static int FindSectionOffset(IByteReader reader, int headerShift, int slotOffset, int targetSectionId)
    {
        for (int i = 0; i < Gen3OffsetMap.SectionCount; i++)
        {
            int sectionStart = headerShift + slotOffset + (i * Gen3OffsetMap.SectionSize);
            ushort sectionId = reader.ReadUInt16(sectionStart + Gen3OffsetMap.SectionIdOffset);

            if (sectionId == targetSectionId)
                return sectionStart;
        }

        throw new InvalidDataException(
            $"Section {targetSectionId} not found in save slot at offset 0x{slotOffset:X}.");
    }

    private static int FindSectionOffsetForWriter(IByteWriter writer, int headerShift, int slotOffset, int targetSectionId)
    {
        for (int i = 0; i < Gen3OffsetMap.SectionCount; i++)
        {
            int sectionStart = headerShift + slotOffset + (i * Gen3OffsetMap.SectionSize);
            ushort sectionId = BinaryPrimitives.ReadUInt16LittleEndian(writer.ReadBytes(sectionStart + Gen3OffsetMap.SectionIdOffset, 2));
            if (sectionId == targetSectionId)
                return sectionStart;
        }
        throw new InvalidDataException($"Section {targetSectionId} not found in save slot at offset 0x{slotOffset:X}.");
    }

    /// <summary>Parses a single 100-byte party Pokémon structure.</summary>
    private Pokemon? ParseSinglePokemon(ReadOnlySpan<byte> data)
    {
        uint pv = data.ReadUInt32LE(Gen3OffsetMap.PkmnPersonalityOffset);
        uint otId = data.ReadUInt32LE(Gen3OffsetMap.PkmnOtIdOffset);

        // Skip empty slots
        if (pv == 0 && otId == 0)
            return null;

        // Decrypt substructure
        ReadOnlySpan<byte> encryptedSub = data.Slice(
            Gen3OffsetMap.PkmnSubstructureOffset,
            Gen3OffsetMap.PkmnSubstructureSize);

        byte[] sub = Gen3Decryptor.DecryptSubstructure(encryptedSub, pv, otId);

        // Block G (Growth) — starts at sub[0]
        ushort species = BinaryPrimitives.ReadUInt16LittleEndian(sub.AsSpan(Gen3OffsetMap.SubG_Species));
        ushort heldItem = BinaryPrimitives.ReadUInt16LittleEndian(sub.AsSpan(Gen3OffsetMap.SubG_HeldItem));
        uint experience = BinaryPrimitives.ReadUInt32LittleEndian(sub.AsSpan(Gen3OffsetMap.SubG_Experience));
        byte friendship = sub[Gen3OffsetMap.SubG_Friendship];

        // Block A (Attacks) — starts at sub[12]
        const int blockA = Gen3OffsetMap.SubstructureBlockSize; // 12
        Move[] moves =
        [
            new(BinaryPrimitives.ReadUInt16LittleEndian(sub.AsSpan(blockA + Gen3OffsetMap.SubA_Move1)),
                sub[blockA + Gen3OffsetMap.SubA_Pp1], 0),
            new(BinaryPrimitives.ReadUInt16LittleEndian(sub.AsSpan(blockA + Gen3OffsetMap.SubA_Move2)),
                sub[blockA + Gen3OffsetMap.SubA_Pp2], 0),
            new(BinaryPrimitives.ReadUInt16LittleEndian(sub.AsSpan(blockA + Gen3OffsetMap.SubA_Move3)),
                sub[blockA + Gen3OffsetMap.SubA_Pp3], 0),
            new(BinaryPrimitives.ReadUInt16LittleEndian(sub.AsSpan(blockA + Gen3OffsetMap.SubA_Move4)),
                sub[blockA + Gen3OffsetMap.SubA_Pp4], 0),
        ];

        // Block E (EVs) — starts at sub[24]
        const int blockE = Gen3OffsetMap.SubstructureBlockSize * 2; // 24
        var evs = new StatBlock(
            Hp: sub[blockE + Gen3OffsetMap.SubE_HpEv],
            Attack: sub[blockE + Gen3OffsetMap.SubE_AtkEv],
            Defense: sub[blockE + Gen3OffsetMap.SubE_DefEv],
            Speed: sub[blockE + Gen3OffsetMap.SubE_SpeEv],
            SpAttack: sub[blockE + Gen3OffsetMap.SubE_SpaEv],
            SpDefense: sub[blockE + Gen3OffsetMap.SubE_SpdEv]);

        // Block M (Miscellaneous) — starts at sub[36]
        const int blockM = Gen3OffsetMap.SubstructureBlockSize * 3; // 36
        byte pokerus = sub[blockM + Gen3OffsetMap.SubM_PokerusStatus];
        byte metLocation = sub[blockM + Gen3OffsetMap.SubM_MetLocation];
        ushort origins = BinaryPrimitives.ReadUInt16LittleEndian(
            sub.AsSpan(blockM + Gen3OffsetMap.SubM_Origins));
        uint ivData = BinaryPrimitives.ReadUInt32LittleEndian(
            sub.AsSpan(blockM + Gen3OffsetMap.SubM_IvsAbility));

        var ivs = StatBlock.FromPackedIvs(ivData);
        bool isEgg = ((ivData >> 30) & 1) == 1;
        byte abilityBit = (byte)((ivData >> 31) & 1);

        // Decode names
        string nickname = data.DecodeGen3String(
            Gen3OffsetMap.PkmnNicknameOffset,
            Gen3OffsetMap.PkmnNicknameLength);
        string otName = data.DecodeGen3String(
            Gen3OffsetMap.PkmnOtNameOffset,
            Gen3OffsetMap.TrainerNameLength);

        ushort publicId = (ushort)(otId & 0xFFFF);
        ushort secretId = (ushort)(otId >> 16);

        // Extract met level and ball from origins
        byte metLevel = (byte)(origins & 0x7F);
        byte ballType = (byte)((origins >> 11) & 0xF);

        return new Pokemon
        {
            PersonalityValue = pv,
            Species = (PokemonSpecies)species,
            Nickname = nickname,
            Level = data.Length >= Gen3OffsetMap.PartyPokemonSize
                ? data[84]  // Level is at offset 84 in party structure
                : (byte)0,
            Experience = experience,
            OriginalTrainer = new TrainerInfo
            {
                Name = otName,
                PublicId = publicId,
                SecretId = secretId
            },
            IVs = ivs,
            EVs = evs,
            Ability = (Ability)abilityBit, // Simplified: just the slot bit
            HeldItem = heldItem,
            Moves = moves,
            IsEgg = isEgg,
            Friendship = friendship,
            PokerusStatus = pokerus,
            MetLocation = metLocation,
            MetLevel = metLevel,
            BallType = ballType
        };
    }

    /// <summary>Detects if the save layout corresponds to FireRed/LeafGreen vs. Ruby/Sapphire.</summary>
    private static bool IsFireRedLeafGreen(ReadOnlySpan<byte> section)
    {
        uint countFRLG = section.ReadUInt32LE(Gen3OffsetMap.PartyCountOffset_FRLG);
        uint countRSE = section.ReadUInt32LE(Gen3OffsetMap.PartyCountOffset_RS);

        if (countFRLG <= 6 && countRSE > 6)
            return true;
        if (countRSE <= 6 && countFRLG > 6)
            return false;

        // Fallback: check if the first Pokémon slot at FRLG offset has non-zero PV/OTID
        if (countFRLG > 0)
        {
            uint pvFRLG = section.ReadUInt32LE(Gen3OffsetMap.PartyDataOffset_FRLG);
            uint otIdFRLG = section.ReadUInt32LE(Gen3OffsetMap.PartyDataOffset_FRLG + 4);
            if (pvFRLG != 0 || otIdFRLG != 0)
                return true;
        }

        return false;
    }

    private static byte ReadBadges(ReadOnlySpan<byte> section2Data, bool isFRLG)
    {
        if (isFRLG)
        {
            // FRLG: all 8 badges are packed in a single byte at offset 884
            return section2Data[884];
        }
        else
        {
            // RSE: Badge 1 is bit 7 of byte 1008, Badges 2-8 are bits 0-6 of byte 1009
            byte b1 = (byte)((section2Data[1008] & 0x80) >> 7);
            byte b2_8 = (byte)((section2Data[1009] & 0x7F) << 1);
            return (byte)(b1 | b2_8);
        }
    }

    private static void WriteBadges(Span<byte> section2Data, byte badges, bool isFRLG)
    {
        if (isFRLG)
        {
            section2Data[884] = badges;
        }
        else
        {
            // RSE:
            // Badge 1 (bit 0 of badges) goes to bit 7 of byte 1008
            byte bit1 = (byte)((badges & 1) << 7);
            section2Data[1008] = (byte)((section2Data[1008] & 0x7F) | bit1);

            // Badges 2-8 (bits 1-7 of badges) go to bits 0-6 of byte 1009
            byte bits2_8 = (byte)((badges >> 1) & 0x7F);
            section2Data[1009] = (byte)((section2Data[1009] & 0x80) | bits2_8);
        }
    }
}
