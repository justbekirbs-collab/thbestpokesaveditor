namespace PokeSaveEditor.Infrastructure.Parsers.Gen3;

using System.Buffers.Binary;
using PokeSaveEditor.Core.Interfaces;

/// <summary>
/// Calculates Gen 3 checksums.
/// The checksum is a 16-bit sum of all 16-bit words in the data,
/// with the upper 16 bits of the 32-bit sum added to the lower 16 bits.
/// </summary>
public sealed class Gen3ChecksumCalculator : IChecksumCalculator
{
    /// <inheritdoc />
    public ushort Calculate(ReadOnlySpan<byte> data)
    {
        uint sum = 0;

        // Sum all 16-bit little-endian words
        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            sum += BinaryPrimitives.ReadUInt16LittleEndian(data[i..]);
        }

        // Fold the upper 16 bits into the lower 16 bits
        return (ushort)((sum & 0xFFFF) + (sum >> 16));
    }

    /// <inheritdoc />
    public bool Validate(ReadOnlySpan<byte> data, ushort storedChecksum) =>
        Calculate(data) == storedChecksum;
}
