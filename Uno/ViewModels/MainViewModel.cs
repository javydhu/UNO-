using System.Collections.ObjectModel;
using Uno.Models;

namespace Uno.ViewModels;

public class MainViewModel : ViewModelBase
{
    private UnoGame _game;
    private string _statusMessage = string.Empty;

    public UnoGame Game
    {
        get => _game;
        set => SetProperty(ref _game, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public MainViewModel()
    {
        _game = new UnoGame();
        UpdateStatus();
    }

    public void PlayCard(Card card)
    {
        if (_game.PlayCard(_game.CurrentPlayer, card))
        {
            UpdateStatus();
            OnPropertyChanged(nameof(Game));
        }
        else
        {
            StatusMessage = $"Movimiento inválido. {Game.CurrentPlayer.Name}, elige una carta válida.";
        }
    }

    public void DrawCard()
    {
        Card drawnCard = _game.DrawCardFromPile();
        _game.CurrentPlayer.Hand.Add(drawnCard);
        _game.NextTurn();
        UpdateStatus();
        OnPropertyChanged(nameof(Game));
    }

    private void UpdateStatus()
    {
        StatusMessage = $"Turno de: {Game.CurrentPlayer.Name} | Color en mesa: {Game.ActiveColor}";
    }
}