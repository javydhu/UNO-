using System;
using Uno.ViewModels;

namespace Uno.Models;

// Una opcion de color que el usuario puede elegir para su nombre
public class PlayerColorOption : ViewModelBase
{
    public string Nombre { get; }
    public string Hex { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    // Mismo patron que Card.PlayAction
    public Action<PlayerColorOption>? SelectAction { get; set; }

    public PlayerColorOption(string nombre, string hex)
    {
        Nombre = nombre;
        Hex = hex;
    }

    public void Seleccionar()
    {
        SelectAction?.Invoke(this);
    }
}
