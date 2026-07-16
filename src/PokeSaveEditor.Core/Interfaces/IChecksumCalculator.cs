namespace PokeSaveEditor.Core.Interfaces;

/// <summary>Calculates and validates checksums for save file integrity.</summary>
public interface IChecksumCalculator
{
    /// <summary>Calculates the checksum for a block of data.</summary>
    ushort Calculate(ReadOnlySpan<byte> data);

    /// <summary>Validates the stored checksum against the calculated one.</summary>
    bool Validate(ReadOnlySpan<byte> data, ushort storedChecksum);
}
