namespace PokeSaveEditor.Core.Models;

using PokeSaveEditor.Core.Enums;

/// <summary>Metadata about a loaded save file.</summary>
public sealed record SaveFileMetadata
{
    /// <summary>Detected game generation / version.</summary>
    public required GameGeneration Generation { get; set; }

    /// <summary>Trainer name from the save header.</summary>
    public required string TrainerName { get; set; }

    /// <summary>Trainer's public ID.</summary>
    public required ushort TrainerId { get; set; }

    /// <summary>Trainer gym badges bitmask.</summary>
    public byte Badges { get; set; }

    /// <summary>Total play time as a TimeSpan.</summary>
    public TimeSpan PlayTime { get; set; }

    /// <summary>File size in bytes.</summary>
    public required long FileSizeBytes { get; set; }

    /// <summary>Original file path.</summary>
    public required string FilePath { get; set; }
}
