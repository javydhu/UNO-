using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Uno.Models;
using Uno.Services;
using System.Collections.Generic;

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
    private int partidaId = 0;
    private bool _hasShoutedUno = false;
    private bool _isPauseMenuVisible = false;

    // El botón de UNO aparece solo si el jugador tiene 2 cartas, al menos una es jugable y aún no lo ha presionado
    public bool CanShoutUno => Game.CurrentPlayer.Hand.Count == 2 
                            && Game.CurrentPlayer.Hand.Any(c => c.IsPlayable) 
                            && !_hasShoutedUno;
    public class JugadorRespuesta
    {
        public string Nombre { get; set; }
        public int Id { get; set; }
        public int PartidasGanadas  { get; set; }
    }

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
    private int _messageToken = 0; // Ayuda a cancelar mensajes viejos si suceden rápido

    public bool HasCardsInDeck => Game.DrawPile.Count > 0;
   
    public string PenaltyButtonText => Game.DrawPile.Count == 0 
        ? "Pasar Turno" 
        : $"Tomar +{PendingDrawCards} y Perder Turno";
    
    private async void ShowTemporaryMessage(string message, int durationMs = 5000)
    {
        int currentToken = ++_messageToken;
        StatusMessage = message;

        await Task.Delay(durationMs);

        if (currentToken == _messageToken)
        {
            UpdateStatus(); 
        }
    }

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

    public async Task StartGame()
    {
        ApiService api = new ApiService();
        List<Player> listaJugadores = new List<Player>();
        List<string> jugadores = new List<string> { "Jugador_1", "Jugador_2", "Jugador_3" };

        string ans = await api.Obtenerdatos("/partida/nueva");
        if (!string.IsNullOrEmpty(ans))
        {
            var datosJson = JsonSerializer.Deserialize<Dictionary<string, int>>(ans);
             partidaId = datosJson != null && datosJson.ContainsKey("partida_id") ? datosJson["partida_id"] : 1;
        }
        
        foreach (var player in jugadores)
        {
            string respuesta = await api.EnviarJugadaLog("/jugadores/login", new { nombre = player });
            if (respuesta != null)
            {
                var jugadorServidor = JsonSerializer.Deserialize<JugadorRespuesta>(respuesta,  
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (jugadorServidor != null)
                {
                    listaJugadores.Add(new Player(jugadorServidor.Id, jugadorServidor.Nombre));
                }
            }
        }
        
        _game = new UnoGame(listaJugadores, partidaId);
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

    public async void PlayCard(Card card)
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
    public bool IsPauseMenuVisible
    {
        get => _isPauseMenuVisible;
        set => SetProperty(ref _isPauseMenuVisible, value);
    }
    public void TogglePauseMenu()
    {
        IsPauseMenuVisible = !IsPauseMenuVisible;
    }

    // Reinicia la partida actual (cierra el menú y llama a StartGame)
    public async Task RestartGame()
    {
        IsPauseMenuVisible = false;
        await StartGame();
    }

    // Abandona la partida y vuelve a la pantalla inicial
    public void QuitToMainMenu()
    {
        IsPauseMenuVisible = false;
        IsGameStarted = false;
        IsGameOver = false;
        // Podrías llamar a un endpoint de la API para avisar que se abandonó la partida
    }

    private async void ExecutePlayCard(Card card, CardColor? chosenColor)
    {
        Player currentPlayer = _game.CurrentPlayer;
        int cardsBeforePlay = currentPlayer.Hand.Count; // Guardamos cuántas cartas tenía antes de jugar

        if (_game.PlayCard(currentPlayer, card, chosenColor))
        {
            string? colorElegidoStr = chosenColor?.ToString();
            await RegistrarJugadaEnServidor(currentPlayer, card , colorElegidoStr);

            // CASTIGO POR NO DECIR UNO
            if (cardsBeforePlay == 2) // Si tenía 2 cartas y jugó una, se quedó con 1
            {
                if (!_hasShoutedUno)
                {
                    await RegistrarUno(currentPlayer);
                    ShowTemporaryMessage($"¡{currentPlayer.Name} olvidó decir UNO! Roba 2 cartas de castigo.", 5000);
                    for (int i = 0; i < 2; i++)
                    {
                        Card? drawn = _game.DrawCardFromPile();
                        if (drawn != null)
                        {
                            currentPlayer.Hand.Add(drawn);
                            await RegistrarRoboEnServidor(currentPlayer, drawn);
                            ShowTemporaryMessage($"{_game.CurrentPlayer.Name} ha robado una carta.", 3000);
                        }
                    }
                }
            }
            _hasShoutedUno = false;

            if (currentPlayer.Hand.Count == 0)
            {
                EndGameWithWinner(currentPlayer.Name, "¡Se ha quedado sin cartas!");
                await RegistrarVictoriaEnServidor(currentPlayer);
                return;
            }

            AssignCardActions();
            UpdateStatus();
            NotifyAllPositions();
        }
        else
        {
            string errorMsg = Game.PendingDrawCards > 0
                ? $"¡Hay un +{Game.PendingDrawCards} acumulado! Responde con +2/+4 o toma el castigo."
                : "Movimiento inválido. Elige una carta del mismo color, número o comodín.";
           
            ShowTemporaryMessage(errorMsg, 5000);
        }
    }

    // BOTÓN ¡UNO!
    public async void ShoutUno()
    {
        _hasShoutedUno = true;
        await RegistrarUno(_game.CurrentPlayer);
        ShowTemporaryMessage($"¡{Game.CurrentPlayer.Name} ha dicho UNO!", 3000);
        NotifyAllPositions(); // Refrescamos para que el botón desaparezca
    }
    public async void DrawCard()
    {
        if (IsGameOver || Game.DrawPile.Count == 0 || HasPendingDraws) return;

        Card? drawnCard = _game.DrawCardFromPile();
        if (drawnCard != null)
        {
            drawnCard.PlayAction = PlayCard;
            _game.CurrentPlayer.Hand.Add(drawnCard);
           
            await RegistrarRoboEnServidor(_game.CurrentPlayer, drawnCard);
            ShowTemporaryMessage($"{_game.CurrentPlayer.Name} ha robado una carta.", 3000);
        }

        NotifyAllPositions();
    }

    public async void TakePenalty()
    {
        if (Game.PendingDrawCards > 0)
        {
            Player victim = Game.CurrentPlayer;
            int count = Game.PendingDrawCards;
            Game.ResolvePendingDraws(victim);
            
            AssignCardActions();
            await RegistrarPenalizacionEnServidor(_game.CurrentPlayer);
            
            _hasShoutedUno = false; // Reset de UNO si se pierde el turno

            ShowTemporaryMessage($"{victim.Name} no pudo responder y robó {count} cartas.", 5000);
           
            NotifyAllPositions();
        }
    }

    public async void PassTurn()
    {
        if (IsGameOver || !CanPassTurn) return;

        Game.ConsecutivePasses++;

        if (Game.ConsecutivePasses >= Game.Players.Count)
        {
            CalculateEndGameByPoints();
            return;
        }
        await RegistrarPaseDeTurnoEnServidor(_game.CurrentPlayer);

        _hasShoutedUno = false; // Reset de UNO

        Game.NextTurn();
        AssignCardActions();
        UpdateStatus();
        NotifyAllPositions();
    }

    private async void CalculateEndGameByPoints()
    {
        var scores = Game.Players.Select(p => new { Player = p, Score = Game.CalculatePlayerPoints(p) })
                                 .OrderBy(x => x.Score)
                                 .ToList();

        var winner = scores.First();
        WinnerName = winner.Player.Name;
        await RegistrarVictoriaEnServidor(winner.Player);


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
                card.IsPlayable = (card.Value == CardValue.DrawTwo || card.Value == CardValue.WildDrawFour);
            }
            else
            {
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
       
        OnPropertyChanged(nameof(HasCardsInDeck));
        OnPropertyChanged(nameof(PenaltyButtonText));

        // Refrescamos el estado del botón de UNO
        OnPropertyChanged(nameof(CanShoutUno));
    }

    private void UpdateStatus()
    {
        _messageToken++; 

        if (Game.PendingDrawCards > 0)
        {
            StatusMessage = $"¡ATENCIÓN {Game.CurrentPlayer.Name}! Hay +{Game.PendingDrawCards} acumulados. ¡Responde con +2/+4 o toma el castigo!";
        }
        else
        {
            StatusMessage = $"Turno de: {Game.CurrentPlayer.Name} | Mazo: {Game.DrawPile.Count} cartas";
        }
    }

    public async Task RegistrarJugadaEnServidor(Player jugador, Card carta, string colorElegido = null)
    {
        ApiService api = new ApiService();
        var datosJugada = new
        {
            jugador_id = jugador.Id,
            partida_id = partidaId,
            color_carta = carta.Color.ToString(),
            valor_carta = carta.Value.ToString(),
            color_elegido = colorElegido 
        };
        string respuesta = await api.EnviarJugadaLog("/partida/jugar-carta", datosJugada);
        if (respuesta != null) Console.WriteLine("Acción registrada en el servidor correctamente.");
    }
    
    public async Task RegistrarVictoriaEnServidor(Player jugador)
    {
        ApiService api = new ApiService();
        var datosJugada = new { jugador_id = jugador.Id, partida_id = partidaId };
        string respuesta = await api.EnviarJugadaLog("/partida/gano", datosJugada);
        if (respuesta != null) Console.WriteLine("Acción registrada en el servidor correctamente.");
    }
    
    public async Task RegistrarRoboEnServidor(Player jugador, Card card)
    {
        ApiService api = new ApiService();
        var datosRobo = new
        {
            jugador_id = jugador.Id, 
            partida_id = partidaId, 
            color_carta= card.Color.ToString() , 
            valor_carta = card.Value.ToString()
        };
        string respuesta = await api.EnviarJugadaLog("/partida/robar-carta", datosRobo);
    }
    
    public async Task RegistrarPenalizacionEnServidor(Player jugador)
    {
        ApiService api = new ApiService();
        await api.EnviarJugadaLog("/partida/tomar-penalizacion", new { jugador_id = jugador.Id, partida_id = partidaId });
    }

    public async Task RegistrarPaseDeTurnoEnServidor(Player jugador)
    {
        ApiService api = new ApiService();
        await api.EnviarJugadaLog("/partida/pasar-turno", new { jugador_id = jugador.Id, partida_id = partidaId });
    }
    
    public async Task RegistrarUno(Player jugador)
    {
        ApiService api = new ApiService();
        await api.EnviarJugadaLog("/partida/uno", new { jugador_id = jugador.Id, partida_id = partidaId, decision=_hasShoutedUno });
    }
}