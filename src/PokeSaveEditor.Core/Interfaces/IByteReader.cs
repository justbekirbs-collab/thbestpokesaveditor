namespace PokeSaveEditor.Core.Interfaces;

/// <summary>
/// Abstraction for reading raw bytes from a save file.
/// Implementations may use FileStream, MemoryMappedFile, or in-memory buffers.
/// </summary>
public interface IByteReader : IDisposable
{
    /// <summary>Total length of the data source in bytes.</summary>
    long Length { get; }

    /// <summary>Reads a contiguous block of bytes starting at the given offset.</summary>
    ReadOnlySpan<byte> ReadBytes(int offset, int count);

    /// <summary>Reads a single byte at the given offset.</summary>
    byte ReadByte(int offset);

    /// <summary>Reads a 16-bit unsigned integer (little-endian) at the given offset.</summary>
    ushort ReadUInt16(int offset);

    /// <summary>Reads a 32-bit unsigned integer (little-endian) at the given offset.</summary>
    uint ReadUInt32(int offset);

    /// <summary>Gets the entire file content as a byte array (for small files).</summary>
    byte[] ToArray();
}
