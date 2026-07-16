using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PokeSaveEditor.UI.Views;

/// <summary>Main application window.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Handles the Exit menu item click.</summary>
    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
