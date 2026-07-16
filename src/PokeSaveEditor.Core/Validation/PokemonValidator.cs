namespace PokeSaveEditor.Core.Validation;

using PokeSaveEditor.Core.Models;

/// <summary>
/// Validates Pokémon data integrity (EV/IV ranges, move legality, etc.).
/// Returns a list of validation errors — empty list means valid.
/// </summary>
public static class PokemonValidator
{
    public const int MaxEvTotal = 510;
    public const int MaxEvSingle = 255;
    public const int MaxIvSingle = 31;
    public const int MaxLevel = 100;
    public const int MinLevel = 1;
    public const int MoveSlotCount = 4;

    /// <summary>Validates a Pokémon and returns any issues found.</summary>
    public static IReadOnlyList<string> Validate(Pokemon pokemon)
    {
        List<string> errors = [];

        // Level range
        if (pokemon.Level is < MinLevel or > MaxLevel)
            errors.Add($"Level {pokemon.Level} is out of range ({MinLevel}–{MaxLevel}).");

        // IV ranges (each must be 0–31)
        ValidateIvStat(errors, "HP", pokemon.IVs.Hp);
        ValidateIvStat(errors, "Attack", pokemon.IVs.Attack);
        ValidateIvStat(errors, "Defense", pokemon.IVs.Defense);
        ValidateIvStat(errors, "Sp.Atk", pokemon.IVs.SpAttack);
        ValidateIvStat(errors, "Sp.Def", pokemon.IVs.SpDefense);
        ValidateIvStat(errors, "Speed", pokemon.IVs.Speed);

        // EV total cap
        if (pokemon.EVs.Total > MaxEvTotal)
            errors.Add($"Total EVs ({pokemon.EVs.Total}) exceed the maximum of {MaxEvTotal}.");

        // Move slots
        if (pokemon.Moves.Length != MoveSlotCount)
            errors.Add($"Expected {MoveSlotCount} move slots, got {pokemon.Moves.Length}.");

        // Species must not be None
        if (pokemon.Species == Enums.PokemonSpecies.None)
            errors.Add("Species cannot be None.");

        return errors;
    }

    private static void ValidateIvStat(List<string> errors, string statName, byte value)
    {
        if (value > MaxIvSingle)
            errors.Add($"IV for {statName} ({value}) exceeds maximum of {MaxIvSingle}.");
    }
}
