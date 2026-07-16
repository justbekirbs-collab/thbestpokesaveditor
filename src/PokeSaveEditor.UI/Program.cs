using Avalonia;

namespace PokeSaveEditor.UI;

/// <summary>Application entry point.</summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>Configures the Avalonia application builder.</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
