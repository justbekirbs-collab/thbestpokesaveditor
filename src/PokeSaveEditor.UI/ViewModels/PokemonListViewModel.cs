using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PokeSaveEditor.Core.Models;

namespace PokeSaveEditor.UI.ViewModels;

/// <summary>ViewModel for the Pokémon list/grid display.</summary>
public partial class PokemonListViewModel : ObservableObject
{
    /// <summary>Observable collection of loaded Pokémon.</summary>
    public ObservableCollection<Pokemon> Pokemon { get; } = [];

    /// <summary>Currently selected Pokémon.</summary>
    [ObservableProperty]
    private Pokemon? _selectedPokemon;

    /// <summary>Number of Pokémon currently loaded.</summary>
    public int Count => Pokemon.Count;

    /// <summary>Replaces the current list with a new set of Pokémon.</summary>
    public void LoadPokemon(IReadOnlyList<Pokemon> pokemon)
    {
        Pokemon.Clear();
        foreach (var p in pokemon)
        {
            Pokemon.Add(p);
        }
        OnPropertyChanged(nameof(Count));
    }

    /// <summary>Clears all loaded Pokémon.</summary>
    public void Clear()
    {
        Pokemon.Clear();
        SelectedPokemon = null;
        OnPropertyChanged(nameof(Count));
    }
}
