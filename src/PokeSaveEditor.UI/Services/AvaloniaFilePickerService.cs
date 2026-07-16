using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace PokeSaveEditor.UI.Services;

/// <summary>
/// Avalonia implementation of <see cref="IFilePickerService"/> using the StorageProvider API.
/// Wraps TopLevel.StorageProvider to keep ViewModels UI-agnostic.
/// </summary>
public sealed class AvaloniaFilePickerService(TopLevel topLevel) : IFilePickerService
{
    private static readonly FilePickerFileType SaveFileType = new("Pokémon Save Files")
    {
        Patterns = ["*.sav", "*.sa1", "*.sa2", "*.srm", "*.dsv"],
        MimeTypes = ["application/octet-stream"]
    };

    /// <inheritdoc />
    public async Task<string?> OpenSaveFileAsync()
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Pokémon Save File",
            AllowMultiple = false,
            FileTypeFilter = [SaveFileType, FilePickerFileTypes.All]
        });

        if (files.Count == 0)
            return null;

        // Get the local file path
        return files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> SaveFileAsync(string suggestedName)
    {
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Pokémon Save File",
            SuggestedFileName = suggestedName,
            FileTypeChoices = [SaveFileType]
        });

        return file?.TryGetLocalPath();
    }
}
