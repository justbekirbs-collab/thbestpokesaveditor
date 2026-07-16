namespace PokeSaveEditor.Core.Enums;

/// <summary>
/// Pokémon natures. Each nature boosts one stat by 10% and lowers another by 10%.
/// Neutral natures (Hardy, Docile, Serious, Bashful, Quirky) have no effect.
/// The nature index is derived from: PersonalityValue % 25.
/// </summary>
public enum Nature : byte
{
    Hardy, Lonely, Brave, Adamant, Naughty,
    Bold, Docile, Relaxed, Impish, Lax,
    Timid, Hasty, Serious, Jolly, Naive,
    Modest, Mild, Quiet, Bashful, Rash,
    Calm, Gentle, Sassy, Careful, Quirky
}
