namespace PokeSaveEditor.Core.Interfaces;

/// <summary>
/// Factory that inspects a save file and returns the appropriate parser.
/// Uses the Strategy pattern — new generations are added by registering new parsers.
/// </summary>
public interface ISaveFileParserFactory
{
    /// <summary>
    /// Analyzes the file header/size and returns the matching parser.
    /// Throws if no parser can handle the format.
    /// </summary>
    ISaveFileParser CreateParser(ReadOnlySpan<byte> fileHeader, long fileSize);

    /// <summary>Returns all registered parser generation names.</summary>
    IReadOnlyList<string> SupportedFormats { get; }
}
