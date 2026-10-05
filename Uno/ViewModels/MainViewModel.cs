using System;
using System.Linq;
using Uno.Models;

namespace Uno.ViewModels;

public class MainViewModel : ViewModelBase
{
    private UnoGame _game;
    private string _statusMessage = string.Empty;
    private bool _isGameStarted = false;
    private bool _isColorPickerVisible = false;
    private bool _isGameOver = false;
    private string _winnerName = string.Empty;
    private string _gameOverDetails = string.Empty;
    private Card? _pendingWildCard = null;

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

    public bool IsColorPickerVisible
    {
        get => _isColorPickerVisible;
        set => SetProperty(ref _isColorPickerVisible, value);
    }

    public bool IsGameOver
    {
        get => _isGameOver;
        set => SetProperty(ref _isGameOver, value);
    }

    public string WinnerName
    {
        get => _winnerName;
        set => SetProperty(ref _winnerName, value);
    }

    public string GameOverDetails
    {
        get => _gameOverDetails;
        set => SetProperty(ref _gameOverDetails, value);
    }

    // Propiedades calculadas para la interfaz
    public int PendingDrawCards => Game.PendingDrawCards;
    public bool HasPendingDraws => Game.PendingDrawCards > 0;
    public bool CanPassTurn => Game.DrawPile.Count == 0 && !HasPendingDraws;

    // Color hexadecimal para el indicador de color activo
    public string ActiveColorHex => Game.ActiveColor switch
    {
        CardColor.Red => "#E74C3C",
        CardColor.Yellow => "#F1C40F",
        CardColor.Blue => "#3498DB",
        CardColor.Green => "#2ECC71",
        _ => "#888888"
    };

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
        _game = new UnoGame();
        IsGameStarted = true;
        IsGameOver = false;

        AssignCardActions();
        UpdateStatus();
        NotifyAllPositions();
    }

    private void AssignCardActions()
    {
        foreach (var player in Game.Players)
        {
            foreach (var card in player.Hand)
            {
                card.PlayAction = PlayCard;
            }
        }
    }

    public void PlayCard(Card card)
    {
        if (IsGameOver) return;

        if (card.Color == CardColor.Wild)
        {
            _pendingWildCard = card;
            IsColorPickerVisible = true;
            return;
        }

        ExecutePlayCard(card, null);
    }

    public void SelectColor(object? parameter)
    {
        if (parameter is string colorName && _pendingWildCard != null)
        {
            if (Enum.TryParse<CardColor>(colorName, out var chosenColor))
            {
                IsColorPickerVisible = false;
                ExecutePlayCard(_pendingWildCard, chosenColor);
                _pendingWildCard = null;
            }
        }
    }

    private void ExecutePlayCard(Card card, CardColor? chosenColor)
    {
        Player currentPlayer = _game.CurrentPlayer;

        if (_game.PlayCard(currentPlayer, card, chosenColor))
        {
            if (currentPlayer.Hand.Count == 0)
            {
                EndGameWithWinner(currentPlayer.Name, "¡Se ha quedado sin cartas!");
                return;
            }

            AssignCardActions();
            UpdateStatus();
            NotifyAllPositions();
        }
        else
        {
            StatusMessage = Game.PendingDrawCards > 0
                ? $"¡Hay un +{Game.PendingDrawCards} acumulado! Responde con +2/+4 o toma el castigo."
                : "Movimiento inválido. Elige una carta del mismo color, número o comodín.";
        }
    }

    public void DrawCard()
    {
        if (IsGameOver || Game.DrawPile.Count == 0 || HasPendingDraws) return;

        Card? drawnCard = _game.DrawCardFromPile();
        if (drawnCard != null)
        {
            drawnCard.PlayAction = PlayCard;
            _game.CurrentPlayer.Hand.Add(drawnCard);
            StatusMessage = $"{_game.CurrentPlayer.Name} ha robado una carta.";
        }

        NotifyAllPositions();
    }

    public void TakePenalty()
    {
        if (Game.PendingDrawCards > 0)
        {
            Player victim = Game.CurrentPlayer;
            int count = Game.PendingDrawCards;
            Game.ResolvePendingDraws(victim);

            AssignCardActions();
            StatusMessage = $"{victim.Name} no pudo responder y robó {count} cartas.";
            NotifyAllPositions();
        }
    }

    public void PassTurn()
    {
        if (IsGameOver || !CanPassTurn) return;

        Game.ConsecutivePasses++;

        if (Game.ConsecutivePasses >= Game.Players.Count)
        {
            CalculateEndGameByPoints();
            return;
        }

        Game.NextTurn();
        AssignCardActions();
        UpdateStatus();
        NotifyAllPositions();
    }

    private void CalculateEndGameByPoints()
    {
        var scores = Game.Players.Select(p => new { Player = p, Score = Game.CalculatePlayerPoints(p) })
                                 .OrderBy(x => x.Score)
                                 .ToList();

        var winner = scores.First();
        WinnerName = winner.Player.Name;

        string details = "PUNTUACIÓN FINAL:\n";
        foreach (var s in scores)
        {
            details += $"{s.Player.Name}: {s.Score} pts\n";
        }

        EndGameWithWinner(WinnerName, details);
    }

    private void EndGameWithWinner(string name, string details)
    {
        WinnerName = name;
        GameOverDetails = details;
        IsGameOver = true;
        StatusMessage = $"¡JUEGO FINALIZADO! Ganador: {WinnerName}";
        NotifyAllPositions();
    }
    
    private void UpdatePlayableCards()
    {
        if (Game == null || Game.Players.Count == 0) return;

        foreach (var card in Game.CurrentPlayer.Hand)
        {
            if (Game.PendingDrawCards > 0)
            {
                // Si hay castigo acumulado, solo puede responder con +2 o +4
                card.IsPlayable = (card.Value == CardValue.DrawTwo || card.Value == CardValue.WildDrawFour);
            }
            else
            {
                // De lo contrario, checamos reglas normales (mismo color, número o comodín)
                card.IsPlayable = card.CanPlayOn(TopCard, Game.ActiveColor);
            }
        }
    }

    private void NotifyAllPositions()
    {
        UpdatePlayableCards();
        OnPropertyChanged(nameof(Game));
        OnPropertyChanged(nameof(BottomPlayer));
        OnPropertyChanged(nameof(RightPlayer));
        OnPropertyChanged(nameof(LeftPlayer));
        OnPropertyChanged(nameof(TopCard));
        OnPropertyChanged(nameof(PendingDrawCards));
        OnPropertyChanged(nameof(HasPendingDraws));
        OnPropertyChanged(nameof(CanPassTurn));
        OnPropertyChanged(nameof(ActiveColorHex));
    }

    private void UpdateStatus()
    {
        if (Game.PendingDrawCards > 0)
        {
            StatusMessage = $"¡ATENCIÓN {Game.CurrentPlayer.Name}! Hay +{Game.PendingDrawCards} acumulados. ¡Responde con +2/+4 o toma el castigo!";
        }
        else
        {
            StatusMessage = $"Turno de: {Game.CurrentPlayer.Name} | Mazo: {Game.DrawPile.Count} cartas";
        }
    }
}