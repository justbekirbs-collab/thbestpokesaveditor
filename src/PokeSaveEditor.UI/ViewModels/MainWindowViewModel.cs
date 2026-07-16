using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeSaveEditor.Core.Enums;
using PokeSaveEditor.Core.Interfaces;
using PokeSaveEditor.Core.Models;
using PokeSaveEditor.Infrastructure.IO;
using PokeSaveEditor.UI.Services;
using PKHeX.Core;
using PokeSaveEditor.Infrastructure.Parsers.PKHeX;

using Nature = PokeSaveEditor.Core.Enums.Nature;
using Ability = PokeSaveEditor.Core.Enums.Ability;
using SaveFileMetadata = PokeSaveEditor.Core.Models.SaveFileMetadata;
using Move = PokeSaveEditor.Core.Models.Move;

namespace PokeSaveEditor.UI.ViewModels;

/// <summary>Main window ViewModel — drives file loading, editing, saving, and selection.</summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFilePickerService _filePicker;
    private readonly ISaveFileParserFactory _parserFactory;

    public MainWindowViewModel(
        IFilePickerService filePicker,
        ISaveFileParserFactory parserFactory)
    {
        _filePicker = filePicker;
        _parserFactory = parserFactory;



        // Synchronize selections between tabs
        PartyList.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PokemonListViewModel.SelectedPokemon) && PartyList.SelectedPokemon is not null)
            {
                SelectedPokemon = PartyList.SelectedPokemon;
                BoxList.SelectedPokemon = null; // Clear other grid selection
            }
        };

        BoxList.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PokemonListViewModel.SelectedPokemon) && BoxList.SelectedPokemon is not null)
            {
                SelectedPokemon = BoxList.SelectedPokemon;
                PartyList.SelectedPokemon = null; // Clear other grid selection
            }
        };
    }

    /// <summary>Party Pokémon list ViewModel.</summary>
    public PokemonListViewModel PartyList { get; } = new();

    /// <summary>PC Box Pokémon list ViewModel.</summary>
    public PokemonListViewModel BoxList { get; } = new();

    /// <summary>Currently selected Pokémon across either grid.</summary>
    [ObservableProperty]
    private Pokemon? _selectedPokemon;

    /// <summary>Metadata about the currently loaded save file.</summary>
    [ObservableProperty]
    private SaveFileMetadata? _metadata;

    /// <summary>Status bar message.</summary>
    [ObservableProperty]
    private string _statusMessage = "Ready — Open a save file to begin.";

    /// <summary>Whether a file is currently loaded.</summary>
    [ObservableProperty]
    private bool _isFileLoaded;

    /// <summary>Window title.</summary>
    public string Title => Metadata is not null
        ? $"PokéSave Editor — {Metadata.TrainerName} ({Metadata.Generation})"
        : "PokéSave Editor";

    // --- Enum binding collections ---
    public IEnumerable<PokemonSpecies> SpeciesList
    {
        get
        {
            var list = Enum.GetValues<PokemonSpecies>().ToList();
            if (SelectedPokemon != null && !list.Contains(SelectedPokemon.Species))
            {
                list.Add(SelectedPokemon.Species);
            }
            return list.OrderBy(x => (ushort)x);
        }
    }
    
    public IReadOnlyList<Nature> NaturesList => Enum.GetValues<Nature>();
    
    public IEnumerable<Ability> AbilitiesList
    {
        get
        {
            var list = Enum.GetValues<Ability>().ToList();
            if (SelectedPokemon != null && !list.Contains(SelectedPokemon.Ability))
            {
                list.Add(SelectedPokemon.Ability);
            }
            return list.OrderBy(x => (byte)x);
        }
    }

    // --- Gym Badge bitmask wrappers ---
    public bool Badge1
    {
        get => Metadata is not null && (Metadata.Badges & 1) != 0;
        set { if (Metadata is not null) { if (value) Metadata.Badges |= 1; else Metadata.Badges &= 0xFE; OnPropertyChanged(); } }
    }
    public bool Badge2
    {
        get => Metadata is not null && (Metadata.Badges & 2) != 0;
        set { if (Metadata is not null) { if (value) Metadata.Badges |= 2; else Metadata.Badges &= 0xFD; OnPropertyChanged(); } }
    }
    public bool Badge3
    {
        get => Metadata is not null && (Metadata.Badges & 4) != 0;
        set { if (Metadata is not null) { if (value) Metadata.Badges |= 4; else Metadata.Badges &= 0xFB; OnPropertyChanged(); } }
    }
    public bool Badge4
    {
        get => Metadata is not null && (Metadata.Badges & 8) != 0;
        set { if (Metadata is not null) { if (value) Metadata.Badges |= 8; else Metadata.Badges &= 0xF7; OnPropertyChanged(); } }
    }
    public bool Badge5
    {
        get => Metadata is not null && (Metadata.Badges & 16) != 0;
        set { if (Metadata is not null) { if (value) Metadata.Badges |= 16; else Metadata.Badges &= 0xEF; OnPropertyChanged(); } }
    }
    public bool Badge6
    {
        get => Metadata is not null && (Metadata.Badges & 32) != 0;
        set { if (Metadata is not null) { if (value) Metadata.Badges |= 32; else Metadata.Badges &= 0xDF; OnPropertyChanged(); } }
    }
    public bool Badge7
    {
        get => Metadata is not null && (Metadata.Badges & 64) != 0;
        set { if (Metadata is not null) { if (value) Metadata.Badges |= 64; else Metadata.Badges &= 0xBF; OnPropertyChanged(); } }
    }
    public bool Badge8
    {
        get => Metadata is not null && (Metadata.Badges & 128) != 0;
        set { if (Metadata is not null) { if (value) Metadata.Badges |= 128; else Metadata.Badges &= 0x7F; OnPropertyChanged(); } }
    }

    private void NotifyBadgeChanges()
    {
        OnPropertyChanged(nameof(Badge1));
        OnPropertyChanged(nameof(Badge2));
        OnPropertyChanged(nameof(Badge3));
        OnPropertyChanged(nameof(Badge4));
        OnPropertyChanged(nameof(Badge5));
        OnPropertyChanged(nameof(Badge6));
        OnPropertyChanged(nameof(Badge7));
        OnPropertyChanged(nameof(Badge8));
    }

    public decimal? TrainerId
    {
        get => Metadata?.TrainerId;
        set { if (Metadata is not null && value.HasValue) { Metadata.TrainerId = (ushort)value.Value; OnPropertyChanged(); } }
    }

    // --- Selected Pokémon Parameter Wrappers ---

    public string? SelectedNickname
    {
        get => SelectedPokemon?.Nickname;
        set { if (SelectedPokemon is not null && value is not null) { SelectedPokemon.Nickname = value; OnPropertyChanged(); } }
    }

    public decimal? SelectedLevel
    {
        get => SelectedPokemon?.Level;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.Level = (byte)value.Value; OnPropertyChanged(); } }
    }

    public PokemonSpecies SelectedSpecies
    {
        get => SelectedPokemon?.Species ?? PokemonSpecies.None;
        set { if (SelectedPokemon is not null) { SelectedPokemon.Species = value; OnPropertyChanged(); } }
    }

    public Ability SelectedAbility
    {
        get => SelectedPokemon?.Ability ?? Ability.None;
        set { if (SelectedPokemon is not null) { SelectedPokemon.Ability = value; OnPropertyChanged(); } }
    }

    public bool SelectedPokemonIsShiny
    {
        get => SelectedPokemon?.IsShiny ?? false;
        set
        {
            if (SelectedPokemon is not null && value != SelectedPokemon.IsShiny)
            {
                ushort pvHigh = (ushort)(SelectedPokemon.PersonalityValue >> 16);
                ushort pvLow = (ushort)(SelectedPokemon.PersonalityValue & 0xFFFF);
                if (value)
                {
                    // Force shininess by adjusting Secret ID to complete the shiny XOR sum
                    SelectedPokemon.OriginalTrainer.SecretId = (ushort)(SelectedPokemon.OriginalTrainer.PublicId ^ pvHigh ^ pvLow);
                }
                else
                {
                    // Remove shininess by introducing offset XOR value 8
                    SelectedPokemon.OriginalTrainer.SecretId = (ushort)(SelectedPokemon.OriginalTrainer.PublicId ^ pvHigh ^ pvLow ^ 8);
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedPokemon));
            }
        }
    }

    public Nature SelectedNature
    {
        get => SelectedPokemon?.Nature ?? Nature.Hardy;
        set
        {
            if (SelectedPokemon is not null && value != SelectedPokemon.Nature)
            {
                uint currentBase = SelectedPokemon.PersonalityValue - (SelectedPokemon.PersonalityValue % 25);
                SelectedPokemon.PersonalityValue = currentBase + (uint)value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedPokemon));
            }
        }
    }

    public decimal? SelectedMove1Id
    {
        get => SelectedPokemon?.Moves[0].MoveId;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.Moves[0] = new Move((ushort)value.Value, 15, 0); OnPropertyChanged(); } }
    }
    public decimal? SelectedMove2Id
    {
        get => SelectedPokemon?.Moves[1].MoveId;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.Moves[1] = new Move((ushort)value.Value, 15, 0); OnPropertyChanged(); } }
    }
    public decimal? SelectedMove3Id
    {
        get => SelectedPokemon?.Moves[2].MoveId;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.Moves[2] = new Move((ushort)value.Value, 15, 0); OnPropertyChanged(); } }
    }
    public decimal? SelectedMove4Id
    {
        get => SelectedPokemon?.Moves[3].MoveId;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.Moves[3] = new Move((ushort)value.Value, 15, 0); OnPropertyChanged(); } }
    }

    public decimal? SelectedIvHp
    {
        get => SelectedPokemon?.IVs.Hp;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.IVs = SelectedPokemon.IVs with { Hp = (byte)value.Value }; OnPropertyChanged(); } }
    }
    public decimal? SelectedIvAtk
    {
        get => SelectedPokemon?.IVs.Attack;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.IVs = SelectedPokemon.IVs with { Attack = (byte)value.Value }; OnPropertyChanged(); } }
    }
    public decimal? SelectedIvDef
    {
        get => SelectedPokemon?.IVs.Defense;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.IVs = SelectedPokemon.IVs with { Defense = (byte)value.Value }; OnPropertyChanged(); } }
    }
    public decimal? SelectedIvSpe
    {
        get => SelectedPokemon?.IVs.Speed;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.IVs = SelectedPokemon.IVs with { Speed = (byte)value.Value }; OnPropertyChanged(); } }
    }
    public decimal? SelectedIvSpa
    {
        get => SelectedPokemon?.IVs.SpAttack;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.IVs = SelectedPokemon.IVs with { SpAttack = (byte)value.Value }; OnPropertyChanged(); } }
    }
    public decimal? SelectedIvSpd
    {
        get => SelectedPokemon?.IVs.SpDefense;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.IVs = SelectedPokemon.IVs with { SpDefense = (byte)value.Value }; OnPropertyChanged(); } }
    }

    public decimal? SelectedEvHp
    {
        get => SelectedPokemon?.EVs.Hp;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.EVs = SelectedPokemon.EVs with { Hp = (byte)value.Value }; OnPropertyChanged(); } }
    }
    public decimal? SelectedEvAtk
    {
        get => SelectedPokemon?.EVs.Attack;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.EVs = SelectedPokemon.EVs with { Attack = (byte)value.Value }; OnPropertyChanged(); } }
    }
    public decimal? SelectedEvDef
    {
        get => SelectedPokemon?.EVs.Defense;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.EVs = SelectedPokemon.EVs with { Defense = (byte)value.Value }; OnPropertyChanged(); } }
    }
    public decimal? SelectedEvSpe
    {
        get => SelectedPokemon?.EVs.Speed;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.EVs = SelectedPokemon.EVs with { Speed = (byte)value.Value }; OnPropertyChanged(); } }
    }
    public decimal? SelectedEvSpa
    {
        get => SelectedPokemon?.EVs.SpAttack;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.EVs = SelectedPokemon.EVs with { SpAttack = (byte)value.Value }; OnPropertyChanged(); } }
    }
    public decimal? SelectedEvSpd
    {
        get => SelectedPokemon?.EVs.SpDefense;
        set { if (SelectedPokemon is not null && value.HasValue) { SelectedPokemon.EVs = SelectedPokemon.EVs with { SpDefense = (byte)value.Value }; OnPropertyChanged(); } }
    }

    partial void OnSelectedPokemonChanged(Pokemon? value)
    {
        OnPropertyChanged(nameof(SelectedNickname));
        OnPropertyChanged(nameof(SelectedLevel));
        OnPropertyChanged(nameof(SelectedSpecies));
        OnPropertyChanged(nameof(SelectedAbility));
        OnPropertyChanged(nameof(SelectedPokemonIsShiny));
        OnPropertyChanged(nameof(SelectedNature));
        OnPropertyChanged(nameof(SelectedMove1Id));
        OnPropertyChanged(nameof(SelectedMove2Id));
        OnPropertyChanged(nameof(SelectedMove3Id));
        OnPropertyChanged(nameof(SelectedMove4Id));
        
        OnPropertyChanged(nameof(SpeciesList));
        OnPropertyChanged(nameof(AbilitiesList));
        
        OnPropertyChanged(nameof(SelectedIvHp));
        OnPropertyChanged(nameof(SelectedIvAtk));
        OnPropertyChanged(nameof(SelectedIvDef));
        OnPropertyChanged(nameof(SelectedIvSpe));
        OnPropertyChanged(nameof(SelectedIvSpa));
        OnPropertyChanged(nameof(SelectedIvSpd));
        
        OnPropertyChanged(nameof(SelectedEvHp));
        OnPropertyChanged(nameof(SelectedEvAtk));
        OnPropertyChanged(nameof(SelectedEvDef));
        OnPropertyChanged(nameof(SelectedEvSpe));
        OnPropertyChanged(nameof(SelectedEvSpa));
        OnPropertyChanged(nameof(SelectedEvSpd));
    }

    // --- File Loading / Parsing ---

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        try
        {
            string? filePath = await _filePicker.OpenSaveFileAsync();
            if (filePath is null) return;

            StatusMessage = $"Loading {Path.GetFileName(filePath)}...";

            using var reader = SaveFileByteReader.Open(filePath);
            ReadOnlySpan<byte> header = reader.ReadBytes(0, Math.Min(8192, (int)reader.Length));

            ISaveFileParser parser = _parserFactory.CreateParser(header, reader.Length);

            // Parse metadata
            Metadata = parser.ParseMetadata(reader, filePath);
            OnPropertyChanged(nameof(Title));
            NotifyBadgeChanges();
            OnPropertyChanged(nameof(TrainerId));

            // Parse party Pokémon
            var party = parser.ParseParty(reader);
            PartyList.LoadPokemon(party);

            // Parse PC Box Pokémon
            var boxes = parser.ParsePcBoxes(reader);
            BoxList.LoadPokemon(boxes);



            IsFileLoaded = true;
            SelectedPokemon = null;
            StatusMessage = $"Loaded {party.Count} party and {boxes.Count} PC Pokémon from {Metadata.TrainerName}'s save.";
        }
        catch (NotSupportedException ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load: {ex.Message}";
        }
    }

    // --- File Saving ---

    [RelayCommand]
    private async Task SaveFileAsync()
    {
        if (Metadata is null || !IsFileLoaded) return;

        try
        {
            string? filePath = await _filePicker.SaveFileAsync(Path.GetFileName(Metadata.FilePath));
            if (filePath is null) return;

            StatusMessage = "Saving changes...";

            byte[] fileData = File.ReadAllBytes(Metadata.FilePath);
            using var writer = new SaveFileByteWriter(fileData, filePath);

            ReadOnlySpan<byte> header = fileData.AsSpan(0, Math.Min(4096, fileData.Length));
            ISaveFileParser parser = _parserFactory.CreateParser(header, fileData.Length);

            // Write metadata (trainer info)
            parser.WriteMetadata(writer, Metadata);

            // Write party Pokémon
            parser.WriteParty(writer, PartyList.Pokemon.ToList());

            // Write PC Box Pokémon
            parser.WritePcBoxes(writer, BoxList.Pokemon.ToList());

            writer.Flush();

            StatusMessage = $"Successfully saved modified save file to {Path.GetFileName(filePath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
        }
    }

    // --- List modification commands ---

    [RelayCommand]
    private void AddToParty()
    {
        if (!IsFileLoaded || Metadata is null) return;
        if (PartyList.Pokemon.Count >= 6)
        {
            StatusMessage = "Party is full (maximum 6 Pokémon).";
            return;
        }

        var newPkmn = new Pokemon
        {
            PersonalityValue = 0x12345678,
            Species = PokemonSpecies.Pikachu,
            Nickname = "PIKACHU",
            Level = 5,
            Experience = 125,
            OriginalTrainer = new TrainerInfo
            {
                Name = Metadata.TrainerName,
                PublicId = Metadata.TrainerId,
                SecretId = 0
            },
            IVs = new StatBlock(31, 31, 31, 31, 31, 31),
            EVs = new StatBlock(0, 0, 0, 0, 0, 0),
            Ability = Ability.Static,
            HeldItem = 0,
            Moves = [new Move(84, 30, 0), new Move(0, 0, 0), new Move(0, 0, 0), new Move(0, 0, 0)]
        };

        PartyList.Pokemon.Add(newPkmn);
        PartyList.SelectedPokemon = newPkmn;
        StatusMessage = "Added default Pikachu to Party.";
    }

    [RelayCommand]
    private void AddToPC()
    {
        if (!IsFileLoaded || Metadata is null) return;
        if (BoxList.Pokemon.Count >= 420)
        {
            StatusMessage = "PC boxes are full (maximum 420 Pokémon).";
            return;
        }

        var newPkmn = new Pokemon
        {
            PersonalityValue = 0x11223344,
            Species = PokemonSpecies.Pikachu,
            Nickname = "PIKACHU",
            Level = 5,
            Experience = 125,
            OriginalTrainer = new TrainerInfo
            {
                Name = Metadata.TrainerName,
                PublicId = Metadata.TrainerId,
                SecretId = 0
            },
            IVs = new StatBlock(31, 31, 31, 31, 31, 31),
            EVs = new StatBlock(0, 0, 0, 0, 0, 0),
            Ability = Ability.Static,
            HeldItem = 0,
            Moves = [new Move(84, 30, 0), new Move(0, 0, 0), new Move(0, 0, 0), new Move(0, 0, 0)]
        };

        BoxList.Pokemon.Add(newPkmn);
        BoxList.SelectedPokemon = newPkmn;
        StatusMessage = "Added default Pikachu to PC Box.";
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedPokemon is null) return;

        if (PartyList.Pokemon.Contains(SelectedPokemon))
        {
            PartyList.Pokemon.Remove(SelectedPokemon);
            StatusMessage = "Removed selected Pokémon from Party.";
        }
        else if (BoxList.Pokemon.Contains(SelectedPokemon))
        {
            BoxList.Pokemon.Remove(SelectedPokemon);
            StatusMessage = "Removed selected Pokémon from PC Box.";
        }

        SelectedPokemon = null;
    }
}
