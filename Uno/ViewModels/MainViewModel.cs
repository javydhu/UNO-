using System.Collections.ObjectModel;
using Uno.Models;

namespace Uno.ViewModels;

public class MainViewModel : ViewModelBase
{
    private UnoGame _game;
    private string _statusMessage = string.Empty;
    private bool _isGameStarted = false;

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

    public bool IsGameStarted
    {
        get => _isGameStarted;
        set => SetProperty(ref _isGameStarted, value);
    }

    // Jugador en turno
    public Player BottomPlayer => Game.CurrentPlayer;

    // Siguiente jugador en la ronda 
    public Player RightPlayer
    {
        get
        {
            int nextIdx = (Game.CurrentPlayerIndex + Game.Direction + Game.Players.Count) % Game.Players.Count;
            return Game.Players[nextIdx];
        }
    }

    // El jugador restante 
    public Player LeftPlayer
    {
        get
        {
            int leftIdx = (Game.CurrentPlayerIndex - Game.Direction + Game.Players.Count) % Game.Players.Count;
            return Game.Players[leftIdx];
        }
    }

    public MainViewModel()
    {
        _game = new UnoGame();
    }

    public void StartGame()
    {
        IsGameStarted = true;
        UpdateStatus();
    }

    public void PlayCard(Card card)
    {
        if (_game.PlayCard(_game.CurrentPlayer, card))
        {
            UpdateStatus();
            NotifyAllPositions();
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
        NotifyAllPositions();
    }

    private void NotifyAllPositions()
    {
        OnPropertyChanged(nameof(Game));
        OnPropertyChanged(nameof(BottomPlayer));
        OnPropertyChanged(nameof(RightPlayer));
        OnPropertyChanged(nameof(LeftPlayer));
    }

    private void UpdateStatus()
    {
        StatusMessage = $"Turno de: {Game.CurrentPlayer.Name} | Color activo: {Game.ActiveColor}";
    }
}