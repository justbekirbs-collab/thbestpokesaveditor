namespace PokeSaveEditor.Core.Models;

/// <summary>
/// Represents a set of six Pokémon stats (used for both IVs and EVs).
/// For IVs: each value is 0–31 (5 bits each, packed into a single uint in GBA format).
/// For EVs: each value is 0–255, with a total cap of 510.
/// </summary>
public readonly record struct StatBlock(
    byte Hp,
    byte Attack,
    byte Defense,
    byte SpAttack,
    byte SpDefense,
    byte Speed)
{
    /// <summary>Sum of all six stat values.</summary>
    public int Total => Hp + Attack + Defense + SpAttack + SpDefense + Speed;

    /// <summary>
    /// Creates a StatBlock by extracting six 5-bit IV values from a packed 32-bit integer.
    /// Bit layout: [4:0]=HP, [9:5]=Atk, [14:10]=Def, [19:15]=Spd, [24:20]=SpAtk, [29:25]=SpDef
    /// </summary>
    public static StatBlock FromPackedIvs(uint packed) => new(
        Hp:        (byte)(packed & 0x1F),
        Attack:    (byte)((packed >> 5) & 0x1F),
        Defense:   (byte)((packed >> 10) & 0x1F),
        Speed:     (byte)((packed >> 15) & 0x1F),
        SpAttack:  (byte)((packed >> 20) & 0x1F),
        SpDefense: (byte)((packed >> 25) & 0x1F));

    /// <summary>Packs six IV values back into a 32-bit integer.</summary>
    public uint ToPackedIvs() =>
        (uint)Hp
        | ((uint)Attack << 5)
        | ((uint)Defense << 10)
        | ((uint)Speed << 15)
        | ((uint)SpAttack << 20)
        | ((uint)SpDefense << 25);

    public override string ToString() =>
        $"HP:{Hp} Atk:{Attack} Def:{Defense} SpA:{SpAttack} SpD:{SpDefense} Spe:{Speed}";
}
