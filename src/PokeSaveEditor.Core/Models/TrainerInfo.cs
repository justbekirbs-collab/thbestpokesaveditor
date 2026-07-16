namespace PokeSaveEditor.Core.Models;

/// <summary>Original Trainer (OT) information for a Pokémon.</summary>
public sealed record TrainerInfo
{
    /// <summary>Original Trainer's visible name (up to 7 characters in Gen 3).</summary>
    public required string Name { get; set; }

    /// <summary>Trainer's public ID (visible in-game, 0–65535).</summary>
    public required ushort PublicId { get; set; }

    /// <summary>Trainer's secret ID (hidden, 0–65535). Used for shiny determination.</summary>
    public required ushort SecretId { get; set; }

    /// <summary>Combined 32-bit Trainer ID (PublicId in low 16 bits, SecretId in high 16 bits).</summary>
    public uint FullId => (uint)(SecretId << 16) | PublicId;
}
