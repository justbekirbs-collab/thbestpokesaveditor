namespace PokeSaveEditor.UI.Services;

/// <summary>Abstraction for platform file picker dialogs.</summary>
public interface IFilePickerService
{
    /// <summary>Opens a file picker dialog and returns the selected file path, or null if cancelled.</summary>
    Task<string?> OpenSaveFileAsync();

    /// <summary>Opens a save dialog and returns the selected output path, or null if cancelled.</summary>
    Task<string?> SaveFileAsync(string suggestedName);
}
