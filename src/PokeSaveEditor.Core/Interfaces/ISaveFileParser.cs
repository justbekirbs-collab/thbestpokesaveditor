namespace PokeSaveEditor.Core.Interfaces;

using PokeSaveEditor.Core.Enums;
using PokeSaveEditor.Core.Models;

/// <summary>
/// Strategy interface for parsing Pokémon save files.
/// Each game generation implements this interface with its own offset map,
/// encryption scheme, and data layout.
/// </summary>
public interface ISaveFileParser
{
    /// <summary>The game generation this parser handles.</summary>
    GameGeneration Generation { get; }

    /// <summary>
    /// Determines if this parser can handle the given file based on header/magic bytes.
    /// </summary>
    bool CanParse(ReadOnlySpan<byte> fileHeader);

    /// <summary>Reads save-level metadata (trainer name, play time, etc.).</summary>
    SaveFileMetadata ParseMetadata(IByteReader reader, string filePath);

    /// <summary>Reads the trainer's party Pokémon (up to 6).</summary>
    IReadOnlyList<Pokemon> ParseParty(IByteReader reader);

    /// <summary>Reads all Pokémon stored in PC boxes.</summary>
    IReadOnlyList<Pokemon> ParsePcBoxes(IByteReader reader);

    /// <summary>Writes modified party Pokémon back to the save file.</summary>
    void WriteParty(IByteWriter writer, IReadOnlyList<Pokemon> party);

    /// <summary>Writes modified PC box Pokémon back to the save file.</summary>
    void WritePcBoxes(IByteWriter writer, IReadOnlyList<Pokemon> pcBoxes);

    /// <summary>Writes modified trainer metadata back to the save file.</summary>
    void WriteMetadata(IByteWriter writer, SaveFileMetadata metadata);
}
