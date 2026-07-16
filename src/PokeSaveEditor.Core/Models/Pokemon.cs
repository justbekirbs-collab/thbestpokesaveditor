namespace PokeSaveEditor.Core.Models;

using PokeSaveEditor.Core.Enums;

/// <summary>
/// Represents a single Pokémon with all editable properties.
/// This is the central domain model used across all generations.
/// Generation-specific parsers map raw bytes into this unified model.
/// </summary>
public sealed class Pokemon
{
    /// <summary>32-bit Personality Value — determines nature, gender, ability slot, shiny status, and substructure shuffle order.</summary>
    public required uint PersonalityValue { get; set; }

    /// <summary>Pokémon species (National Pokédex number).</summary>
    public required PokemonSpecies Species { get; set; }

    /// <summary>The Pokémon's nickname (up to 10 chars in Gen 3).</summary>
    public required string Nickname { get; set; }

    /// <summary>Current level (1–100).</summary>
    public required byte Level { get; set; }

    /// <summary>Total accumulated experience points.</summary>
    public required uint Experience { get; set; }

    /// <summary>Original Trainer information.</summary>
    public required TrainerInfo OriginalTrainer { get; set; }

    /// <summary>Individual Values — genetic stats (each 0–31).</summary>
    public required StatBlock IVs { get; set; }

    /// <summary>Effort Values — trained stats (each 0–255, total ≤ 510).</summary>
    public required StatBlock EVs { get; set; }

    /// <summary>The Pokémon's ability.</summary>
    public required Ability Ability { get; set; }

    /// <summary>Held item ID (0 = no item).</summary>
    public ushort HeldItem { get; set; }

    /// <summary>Four move slots.</summary>
    public Move[] Moves { get; set; } = new Move[4];

    /// <summary>Whether this Pokémon is an unhatched egg.</summary>
    public bool IsEgg { get; set; }

    /// <summary>Friendship / happiness value (0–255).</summary>
    public byte Friendship { get; set; }

    /// <summary>Pokérus status byte.</summary>
    public byte PokerusStatus { get; set; }

    /// <summary>Met location ID.</summary>
    public ushort MetLocation { get; set; }

    /// <summary>Level at which this Pokémon was caught.</summary>
    public byte MetLevel { get; set; }

    /// <summary>Ball used to catch this Pokémon.</summary>
    public byte BallType { get; set; }

    // --- Computed Properties ---

    /// <summary>Nature derived from PersonalityValue % 25.</summary>
    public Nature Nature => (Nature)(PersonalityValue % 25);

    /// <summary>
    /// Determines if the Pokémon is shiny.
    /// Formula: (TrainerPublicId XOR TrainerSecretId XOR PV_High16 XOR PV_Low16) &lt; 8
    /// </summary>
    public bool IsShiny
    {
        get
        {
            ushort pvHigh = (ushort)(PersonalityValue >> 16);
            ushort pvLow = (ushort)(PersonalityValue & 0xFFFF);
            uint shinyValue = (uint)(OriginalTrainer.PublicId ^ OriginalTrainer.SecretId ^ pvHigh ^ pvLow);
            return shinyValue < 8;
        }
    }

    /// <summary>Ability slot (0 or 1) determined by the lowest bit of the Personality Value.</summary>
    public int AbilitySlot => (int)(PersonalityValue & 1);

    /// <summary>Gender determination value (lower 8 bits of PV, compared against species threshold).</summary>
    public byte GenderValue => (byte)(PersonalityValue & 0xFF);

    public override string ToString() => $"{Species} (Lv.{Level}) \"{Nickname}\" [{Nature}]";
}
