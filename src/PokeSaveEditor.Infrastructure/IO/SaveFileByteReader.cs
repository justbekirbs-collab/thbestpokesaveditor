namespace PokeSaveEditor.Infrastructure.IO;

using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using PokeSaveEditor.Core.Interfaces;

/// <summary>
/// High-performance byte reader for save files.
/// Uses MemoryMappedFile for large files (>64KB) and in-memory byte[] for small files.
/// All multi-byte reads use BinaryPrimitives for explicit little-endian handling.
/// </summary>
public sealed class SaveFileByteReader : IByteReader
{
    private const int MemoryMappedThreshold = 65_536; // 64KB

    private readonly byte[] _data;
    private bool _disposed;

    private SaveFileByteReader(byte[] data)
    {
        _data = data;
    }

    /// <summary>Opens a save file for reading.</summary>
    public static SaveFileByteReader Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException("Save file not found.", filePath);

        byte[] data;

        if (fileInfo.Length > MemoryMappedThreshold)
        {
            // Use MemoryMappedFile for zero-copy initial read of large files
            using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using var accessor = mmf.CreateViewStream(0, fileInfo.Length, MemoryMappedFileAccess.Read);
            data = new byte[fileInfo.Length];
            accessor.ReadExactly(data);
        }
        else
        {
            data = File.ReadAllBytes(filePath);
        }

        return new SaveFileByteReader(data);
    }

    /// <summary>Creates a reader from an existing byte array (useful for testing).</summary>
    public static SaveFileByteReader FromBytes(byte[] data) => new(data);

    /// <inheritdoc />
    public long Length => _data.Length;

    /// <inheritdoc />
    public ReadOnlySpan<byte> ReadBytes(int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateRange(offset, count);
        return _data.AsSpan(offset, count);
    }

    /// <inheritdoc />
    public byte ReadByte(int offset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateRange(offset, 1);
        return _data[offset];
    }

    /// <inheritdoc />
    public ushort ReadUInt16(int offset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateRange(offset, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(offset, 2));
    }

    /// <inheritdoc />
    public uint ReadUInt32(int offset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateRange(offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset, 4));
    }

    /// <inheritdoc />
    public byte[] ToArray()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return (byte[])_data.Clone();
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void ValidateRange(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > _data.Length)
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                $"Read at offset {offset} with count {count} exceeds data length {_data.Length}.");
    }
}
