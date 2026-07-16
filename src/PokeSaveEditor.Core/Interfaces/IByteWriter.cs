namespace PokeSaveEditor.Core.Interfaces;

/// <summary>Abstraction for writing modified bytes back to a save file.</summary>
public interface IByteWriter : IDisposable
{
    /// <summary>Total length of the save buffer in bytes.</summary>
    long Length { get; }

    /// <summary>Writes a block of bytes at the given offset.</summary>
    void WriteBytes(int offset, ReadOnlySpan<byte> data);

    /// <summary>Writes a single byte at the given offset.</summary>
    void WriteByte(int offset, byte value);

    /// <summary>Writes a 16-bit unsigned integer (little-endian) at the given offset.</summary>
    void WriteUInt16(int offset, ushort value);

    /// <summary>Writes a 32-bit unsigned integer (little-endian) at the given offset.</summary>
    void WriteUInt32(int offset, uint value);

    /// <summary>Reads a block of bytes from the writer's buffer (used to recalculate checksums).</summary>
    ReadOnlySpan<byte> ReadBytes(int offset, int count);

    /// <summary>Flushes all pending writes to the underlying storage.</summary>
    void Flush();
}
