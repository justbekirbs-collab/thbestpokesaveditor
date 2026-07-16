namespace PokeSaveEditor.Infrastructure.Factories;

using PokeSaveEditor.Core.Interfaces;
using PokeSaveEditor.Infrastructure.Parsers.Gen3;
using PokeSaveEditor.Infrastructure.Parsers.PKHeX;

/// <summary>
/// Factory that auto-detects save file format and returns the appropriate parser.
/// New generations are added by registering additional parsers in the chain.
/// </summary>
public sealed class SaveFileParserFactory : ISaveFileParserFactory
{
    private readonly IReadOnlyList<ISaveFileParser> _parsers;

    public SaveFileParserFactory()
    {
        // Register all known parsers. Order matters for ambiguous formats.
        _parsers =
        [
            new PKHeXSaveFileParser(),
            new Gen3SaveFileParser(),
            // Future: new Gen4SaveFileParser(),
            // Future: new Gen5SaveFileParser(),
        ];
    }

    /// <summary>Allows custom parser injection (e.g., for ROM hacks).</summary>
    public SaveFileParserFactory(IEnumerable<ISaveFileParser> parsers)
    {
        _parsers = parsers.ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedFormats =>
        _parsers.Select(p => p.Generation.ToString()).ToList();

    /// <inheritdoc />
    public ISaveFileParser CreateParser(ReadOnlySpan<byte> fileHeader, long fileSize)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(fileHeader))
                return parser;
        }

        throw new NotSupportedException(
            $"No parser found for save file (size: {fileSize} bytes). " +
            $"Supported formats: {string.Join(", ", SupportedFormats)}");
    }
}
