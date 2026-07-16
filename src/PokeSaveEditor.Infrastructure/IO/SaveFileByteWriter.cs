namespace PokeSaveEditor.Infrastructure.IO;

using System.Buffers.Binary;
using PokeSaveEditor.Core.Interfaces;

/// <summary>
/// Byte writer that operates on an in-memory buffer and flushes to disk on demand.
/// Modifications are accumulated in memory and written atomically via <see cref="Flush"/>.
/// </summary>
public sealed class SaveFileByteWriter : IByteWriter
{
    private readonly byte[] _data;
    private readonly string _filePath;
    private bool _disposed;

    /// <summary>Creates a writer from the given source data, targeting the specified output path.</summary>
    public SaveFileByteWriter(byte[] sourceData, string filePath)
    {
        _data = (byte[])sourceData.Clone();
        _filePath = filePath;
    }

    /// <inheritdoc />
    public long Length => _data.Length;

    /// <inheritdoc />
    public void WriteBytes(int offset, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        data.CopyTo(_data.AsSpan(offset));
    }

    /// <inheritdoc />
    public void WriteByte(int offset, byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _data[offset] = value;
    }

    /// <inheritdoc />
    public void WriteUInt16(int offset, ushort value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(offset, 2), value);
    }

    /// <inheritdoc />
    public void WriteUInt32(int offset, uint value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan(offset, 4), value);
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> ReadBytes(int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _data.AsSpan(offset, count);
    }

    /// <inheritdoc />
    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Atomic write: write to temp file, then replace original
        var tempPath = _filePath + ".tmp";
        File.WriteAllBytes(tempPath, _data);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
