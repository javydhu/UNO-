using System.Collections.ObjectModel;
using Uno.Models;
using System;

namespace Uno.ViewModels;

public class MainViewModel : ViewModelBase
{
    private UnoGame _game;
    private string _statusMessage = string.Empty;
    private bool _isGameStarted = false;
    private bool _hasPlayedCardThisTurn = false;
    public Card TopCard => Game.TopCard;

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

    public Player BottomPlayer => Game.CurrentPlayer;

    public Player RightPlayer
    {
        get
        {
            int nextIdx = (Game.CurrentPlayerIndex + Game.Direction + Game.Players.Count) % Game.Players.Count;
            return Game.Players[nextIdx];
        }
    }

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
        _hasPlayedCardThisTurn = false;
        
        foreach (var player in Game.Players)
        {
            foreach (var card in player.Hand)
            {
                card.PlayAction = PlayCard;
            }
        }
        
        UpdateStatus();
    }
    
    public void PlayCard(Card card)
    {
        Console.WriteLine($"[DEBUG] Clic detectado en la carta: {card.Color} {card.Value}");
        // 1. Evitar que tire más de una carta
        if (_hasPlayedCardThisTurn)
        {
            StatusMessage = "Ya jugaste una carta. Presiona ENTER para pasar el turno.";
            return;
        }

        // 2. Intentar jugar la carta
        if (_game.PlayCard(_game.CurrentPlayer, card))
        {
            _hasPlayedCardThisTurn = true; // Se bloquea la mano
            StatusMessage = $"Carta en mesa. Presiona ENTER para finalizar tu turno. | Color: {_game.ActiveColor}";
            NotifyAllPositions(); // Actualizar la vista (mostrar la nueva TopCard)
        }
        else
        {
            StatusMessage = $"Movimiento inválido. Elige una carta del mismo color, número o un comodín.";
        }
    }

    public void DrawCard()
    {
        if (_hasPlayedCardThisTurn)
        {
            StatusMessage = "Ya jugaste una carta, no puedes robar. Presiona ENTER.";
            return;
        }

        Card drawnCard = _game.DrawCardFromPile();

        drawnCard.PlayAction = PlayCard; 
        
        _game.CurrentPlayer.Hand.Add(drawnCard);
        
        UpdateStatus();
        NotifyAllPositions();
    }
    
    public void EndTurn()
    {
        if (!IsGameStarted) return;

        // Validar que haya puesto una carta antes de pasar
        if (!_hasPlayedCardThisTurn)
        {
            StatusMessage = "¡Debes jugar una carta antes de presionar ENTER!";
            return;
        }

        // Avanzar el turno en la lógica del juego
        _game.NextTurn();
        
        // Resetear la variable para el nuevo jugador
        _hasPlayedCardThisTurn = false;
        
        UpdateStatus();
        NotifyAllPositions();
    }

    private void NotifyAllPositions()
    {
        OnPropertyChanged(nameof(Game));
        OnPropertyChanged(nameof(BottomPlayer));
        OnPropertyChanged(nameof(RightPlayer));
        OnPropertyChanged(nameof(LeftPlayer));
        OnPropertyChanged(nameof(TopCard));
    }

    private void UpdateStatus()
    {
        if (!_hasPlayedCardThisTurn)
        {
            StatusMessage = $"Turno de: {Game.CurrentPlayer.Name} | Color activo: {Game.ActiveColor}";
        }
    }
}