namespace PokeSaveEditor.Infrastructure.Extensions;

using System.Buffers.Binary;
using System.Text;

/// <summary>Extension methods for efficient byte span operations.</summary>
public static class SpanExtensions
{
    /// <summary>Reads a little-endian uint16 from the span at the given offset.</summary>
    public static ushort ReadUInt16LE(this ReadOnlySpan<byte> span, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]);

    /// <summary>Reads a little-endian uint32 from the span at the given offset.</summary>
    public static uint ReadUInt32LE(this ReadOnlySpan<byte> span, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(span[offset..]);

    /// <summary>
    /// Decodes a Gen 3 encoded string from raw bytes.
    /// Gen 3 uses a custom character encoding (0xFF = terminator).
    /// This is a simplified version supporting ASCII-range characters.
    /// </summary>
    public static string DecodeGen3String(this ReadOnlySpan<byte> span, int offset, int maxLength)
    {
        var sb = new StringBuilder(maxLength);

        for (int i = 0; i < maxLength; i++)
        {
            byte b = span[offset + i];
            if (b == 0xFF) break; // String terminator

            // Gen 3 character table: 0xBB='A', 0xD5='a', 0xA1='0', spaces/special mapped
            char c = b switch
            {
                >= 0xBB and <= 0xD4 => (char)('A' + (b - 0xBB)),  // A-Z
                >= 0xD5 and <= 0xEE => (char)('a' + (b - 0xD5)),  // a-z
                >= 0xA1 and <= 0xAA => (char)('0' + (b - 0xA1)),  // 0-9
                0x00 => ' ',
                0xAB => '!',
                0xAC => '?',
                0xAD => '.',
                0xAE => '-',
                0xB0 => '\u2026',  // ellipsis
                0xB1 => '\u201C',  // left double quote
                0xB2 => '\u201D',  // right double quote
                0xB3 => '\u2018',  // left single quote
                0xB4 => '\u2019',  // right single quote
                _ => '?'
            };
            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>Extracts N bits starting at the given bit position from a uint.</summary>
    public static uint ExtractBits(this uint value, int startBit, int bitCount) =>
        (value >> startBit) & ((1u << bitCount) - 1);
}
