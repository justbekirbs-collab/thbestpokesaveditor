using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PokeSaveEditor.Core.Interfaces;
using PokeSaveEditor.Infrastructure;
using PokeSaveEditor.UI.Services;
using PokeSaveEditor.UI.ViewModels;
using PokeSaveEditor.UI.Views;

namespace PokeSaveEditor.UI;

/// <summary>Avalonia application with DI container setup.</summary>
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();

            // Build DI container
            var services = new ServiceCollection();
            services.AddInfrastructure();
            services.AddSingleton<IFilePickerService>(
                _ => new AvaloniaFilePickerService(mainWindow));
            services.AddSingleton<PokemonListViewModel>();
            services.AddSingleton<MainWindowViewModel>();
            var provider = services.BuildServiceProvider();

            // Wire up the main window
            mainWindow.DataContext = provider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
