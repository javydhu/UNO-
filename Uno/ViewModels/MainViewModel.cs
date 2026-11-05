using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO;
using System.Text.Json;

using Avalonia.Threading;

using Uno.Shared.Models;
using Uno.Services;

namespace Uno.ViewModels;

public class MainViewModel : ViewModelBase
{
    private GameClient? _client;

    private CancellationTokenSource? _receiveCancellation;

    private Task? _receiveTask;

    private int _myPlayerId = 0;

    private string _serverIp = "127.0.0.1";

    private string _serverPort = "5000";

    private string _statusMessage = string.Empty;

    public string ServerIp
    {
        get => _serverIp;
        set => SetProperty(ref _serverIp, value);
    }

    public string ServerPort
    {
        get => _serverPort;
        set => SetProperty(ref _serverPort, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // Navegacion entre las pantallas

    private bool _isConnectionScreenVisible = true;

    private bool _isPlayerSelectionVisible;

    private bool _isWaitingRoomVisible;

    private bool _isGameStarted;

    private bool _isGameOver;

    public bool IsConnectionScreenVisible
    {
        get => _isConnectionScreenVisible;
        set => SetProperty(ref _isConnectionScreenVisible, value);
    }

    public bool IsPlayerSelectionVisible
    {
        get => _isPlayerSelectionVisible;
        set => SetProperty(ref _isPlayerSelectionVisible, value);
    }

    public bool IsWaitingRoomVisible
    {
        get => _isWaitingRoomVisible;
        set => SetProperty(ref _isWaitingRoomVisible, value);
    }

    public bool IsGameStarted
    {
        get => _isGameStarted;
        set => SetProperty(ref _isGameStarted, value);
    }

    public bool IsGameOver
    {
        get => _isGameOver;
        set => SetProperty(ref _isGameOver, value);
    }

    // Seleccion de jugadores

    public ObservableCollection<RegisteredPlayerDto>
        RegisteredPlayers { get; } = new();

    private RegisteredPlayerDto? _selectedRegisteredPlayer;

    public RegisteredPlayerDto? SelectedRegisteredPlayer
    {
        get => _selectedRegisteredPlayer;
        set => SetProperty(
            ref _selectedRegisteredPlayer,
            value);
    }

    private string _newPlayerName = string.Empty;

    public string NewPlayerName
    {
        get => _newPlayerName;
        set => SetProperty(
            ref _newPlayerName,
            value);
    }

    // Sala de espera

    public ObservableCollection<PlayerPublicState>
        WaitingRoomPlayers { get; } = new();

    private int _connectedPlayers;

    private int _requiredPlayers = 3;

    public string WaitingRoomCountText =>
        $"{_connectedPlayers} / {_requiredPlayers} jugadores";

    // Partida

    private Card? _topCard;

    private PlayerDisplayModel? _bottomPlayer;

    private PlayerDisplayModel? _leftPlayer;

    private PlayerDisplayModel? _rightPlayer;

    private string _winnerName = string.Empty;

    private string _gameOverDetails = string.Empty;

    private int _drawPileCount;

    private int _pendingDrawCards;

    private int _currentPlayerId;

    private int _direction = 1;

    private bool _hasShoutedUno;

    private bool _isMyTurn;

    private Card? _pendingWildCard;

    private bool _isColorPickerVisible;

    private bool _isPauseMenuVisible;

    public Card? TopCard
    {
        get => _topCard;
        set => SetProperty(ref _topCard, value);
    }

    public PlayerDisplayModel? BottomPlayer
    {
        get => _bottomPlayer;
        set => SetProperty(ref _bottomPlayer, value);
    }

    public PlayerDisplayModel? LeftPlayer
    {
        get => _leftPlayer;
        set => SetProperty(ref _leftPlayer, value);
    }

    public PlayerDisplayModel? RightPlayer
    {
        get => _rightPlayer;
        set => SetProperty(ref _rightPlayer, value);
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

    public bool IsColorPickerVisible
    {
        get => _isColorPickerVisible;
        set => SetProperty(ref _isColorPickerVisible, value);
    }

    public bool IsPauseMenuVisible
    {
        get => _isPauseMenuVisible;
        set => SetProperty(ref _isPauseMenuVisible, value);
    }

    public bool IsMyTurn
    {
        get => _isMyTurn;
        set => SetProperty(ref _isMyTurn, value);
    }

    public string BottomPlayerLabel =>
        BottomPlayer == null
            ? string.Empty
            : IsMyTurn
                ? $"TU TURNO: {BottomPlayer.Name}"
                : $"TU JUGADOR: {BottomPlayer.Name}";

    public int PendingDrawCards
    {
        get => _pendingDrawCards;
        set => SetProperty(ref _pendingDrawCards, value);
    }

    public bool HasPendingDraws =>
        IsMyTurn && PendingDrawCards > 0;

    public bool HasCardsInDeck =>
        _drawPileCount > 0;

    public bool CanDrawCard =>
        IsMyTurn &&
        _drawPileCount > 0 &&
        PendingDrawCards == 0;

    public bool CanPassTurn =>
        IsMyTurn &&
        _drawPileCount == 0 &&
        PendingDrawCards == 0;

    public bool CanShoutUno =>
        IsMyTurn &&
        BottomPlayer is { Hand.Count: 2 } bottom &&
        bottom.Hand.Any(card => card.IsPlayable) &&
        !_hasShoutedUno;

    public string PenaltyButtonText =>
        $"Tomar +{PendingDrawCards} y Perder Turno";

    public string ActiveColorHex { get; private set; } =
        "#888888";

    // Comandos

    public ICommand ConnectCommand { get; }

    public ICommand JoinSelectedPlayerCommand { get; }

    public ICommand RegisterNewPlayerCommand { get; }

    public ICommand CancelWaitingCommand { get; }

    public ICommand PlayCardCommand { get; }

    public ICommand DrawCardCommand { get; }

    public ICommand TakePenaltyCommand { get; }

    public ICommand PassTurnCommand { get; }

    public ICommand ShoutUnoCommand { get; }

    public ICommand SelectColorCommand { get; }

    public ICommand TogglePauseMenuCommand { get; }

    public ICommand ExitToMenuCommand { get; }

    public MainViewModel()
    {
        ConnectCommand = new AsyncCommand(
            _ => ConnectAsync());

        JoinSelectedPlayerCommand = new AsyncCommand(
            _ => JoinSelectedPlayerAsync());

        RegisterNewPlayerCommand = new AsyncCommand(
            _ => RegisterNewPlayerAsync());

        CancelWaitingCommand = new AsyncCommand(
            _ => CancelWaitingAsync());

        PlayCardCommand = new AsyncCommand(
            p => p is Card card
                ? PlayCardAsync(card)
                : Task.CompletedTask);

        DrawCardCommand = new AsyncCommand(
            _ => SendActionAsync("draw_card"));

        TakePenaltyCommand = new AsyncCommand(
            _ => SendActionAsync("resolve_penalty"));

        PassTurnCommand = new AsyncCommand(
            _ => SendActionAsync("pass_turn"));

        ShoutUnoCommand = new AsyncCommand(
            _ => SendActionAsync("shout_uno"));

        SelectColorCommand = new AsyncCommand(
            p => SelectColorAsync(p?.ToString()));

        TogglePauseMenuCommand = new RelayCommand(
            _ => IsPauseMenuVisible = !IsPauseMenuVisible);

        ExitToMenuCommand = new AsyncCommand(
            _ => ExitToMenuAsync());
    }

    // Conectar al servidor

    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerIp))
        {
            StatusMessage = "Introduce la IP del servidor.";
            return;
        }

        if (!int.TryParse(ServerPort, out int port) ||
            port < 1 || port > 65535)
        {
            StatusMessage = "El puerto no es válido.";
            return;
        }

        try
        {
            StatusMessage = "Conectando al servidor...";

            GameClient client = new GameClient();

            await client.ConnectAsync(
                ServerIp.Trim(),
                port);

            _client = client;

            _receiveCancellation =
                new CancellationTokenSource();

            _receiveTask = ReceiveMessagesAsync(
                _receiveCancellation.Token);

            // Solicitar los jugadores registrados.
            await client.SendMessageAsync(
                "list_players",
                new { });

            StatusMessage =
                "Conectado. Consultando jugadores...";

        }
        catch (Exception ex)
        {
            StatusMessage =
                $"No se pudo conectar: {ex.Message}";

            IsConnectionScreenVisible = true;
        }
    }

    // Recibir mensajes del servidor

    private async Task ReceiveMessagesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_client == null)
                    break;

                NetworkMessage? message =
                    await _client.ReadMessageAsync(
                        cancellationToken);

                if (message == null)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            StatusMessage = "Se perdió la conexión con el servidor.";

                            IsGameStarted = false;
                            IsGameOver = false;
                            IsWaitingRoomVisible = false;
                            IsPlayerSelectionVisible = false;
                            IsConnectionScreenVisible = true;
                        });
                    }

                    break;
                }
                Dispatcher.UIThread.Post(
                    () => HandleServerMessage(message));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusMessage =
                    $"Se perdió la conexión: {ex.Message}";
            });
        }
    }

    // Interpretar mensajes

    private void HandleServerMessage(
        NetworkMessage message)
    {
        switch (message.Type)
        {
            //Lista de jugadores

            case "players_list":
            {
                var players =
                    message.GetData<List<RegisteredPlayerDto>>();

                if (players == null)
                    return;

                RegisteredPlayers.Clear();

                foreach (var player in players)
                {
                    RegisteredPlayers.Add(player);
                }

                IsConnectionScreenVisible = false;
                IsPlayerSelectionVisible = true;
                IsWaitingRoomVisible = false;

                StatusMessage =
                    "Selecciona un jugador o registra uno nuevo.";

                break;
            }

            case "join_result":
            {
                JoinResult? result =
                    message.GetData<JoinResult>();

                if (result == null)
                    return;

                StatusMessage = result.Message;

                if (result.Accepted)
                {
                    _myPlayerId = result.PlayerId ?? 0;

                    _connectedPlayers =
                        result.ConnectedPlayers;

                    _requiredPlayers =
                        result.RequiredPlayers;

                    IsPlayerSelectionVisible = false;
                    IsWaitingRoomVisible = true;

                    OnPropertyChanged(nameof(WaitingRoomCountText));
                }

                break;
            }

            // Actualizacion de sala

            case "waiting_room_update":
            {
                WaitingRoomSnapshot? snapshot =
                    message.GetData<WaitingRoomSnapshot>();

                if (snapshot == null)
                    return;

                _connectedPlayers =
                    snapshot.ConnectedPlayers;

                _requiredPlayers =
                    snapshot.RequiredPlayers;

                WaitingRoomPlayers.Clear();

                foreach (var player in snapshot.Players)
                {
                    WaitingRoomPlayers.Add(player);
                }

                OnPropertyChanged(nameof(WaitingRoomCountText));

                break;
            }

            // Resultado de la accion

            case "action_result":
            {
                ActionResultMessage? result =
                    message.GetData<ActionResultMessage>();

                if (result == null)
                    return;

                StatusMessage = result.Message;

                break;
            }

            // Estado de la partida

            case "game_state":
            {
                GameStateSnapshot? snapshot =
                    message.GetData<GameStateSnapshot>();

                if (snapshot != null)
                {
                    ApplyGameState(snapshot);
                }

                break;
            }

            // Cancelar sala

            case "leave_room_result":
            {
                ActionResultMessage? result =
                    message.GetData<ActionResultMessage>();

                if (result == null)
                    return;

                StatusMessage = result.Message;

                if (result.Accepted)
                {
                    IsWaitingRoomVisible = false;
                    IsPlayerSelectionVisible = true;

                    WaitingRoomPlayers.Clear();

                    RegisteredPlayers.Clear();

                    _ = _client?.SendMessageAsync(
                        "list_players",
                        new { });
                }

                break;
            }
            

            case "server_error":
            {
                ActionResultMessage? error =
                    message.GetData<ActionResultMessage>();

                StatusMessage =
                    error?.Message ?? "Error del servidor.";

                break;
            }
        }
    }

    // Seleccionar jugador registrado

    private async Task JoinSelectedPlayerAsync()
    {
        if (SelectedRegisteredPlayer == null)
        {
            StatusMessage =
                "Selecciona un jugador de la lista.";

            return;
        }

        await JoinRoomAsync(
            SelectedRegisteredPlayer.Nombre);
    }

    // Registrar jugador nuevo

    private async Task RegisterNewPlayerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPlayerName))
        {
            StatusMessage =
                "Escribe el nombre del nuevo jugador.";

            return;
        }

        await JoinRoomAsync(
            NewPlayerName.Trim());
    }

    // Entrar a la sala

    private async Task JoinRoomAsync(string name)
    {
        try
        {
            if (_client == null)
                throw new InvalidOperationException(
                    "No estás conectado al servidor.");

            await _client.SendMessageAsync(
                "join",
                new JoinRequest
                {
                    Name = name
                });

            StatusMessage = "Solicitando entrada a la sala...";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    // Cancelar sala de espera

    private async Task CancelWaitingAsync()
    {
        try
        {
            if (_client == null)
                return;

            await _client.SendMessageAsync(
                "leave_room",
                new { });

            StatusMessage = "Saliendo de la sala...";
        }
        catch (Exception ex)
        {
            StatusMessage =
                $"No se pudo salir de la sala: {ex.Message}";
        }
    }

    // Actualizar estado de partida

    private void ApplyGameState(
        GameStateSnapshot snapshot)
    {
        _currentPlayerId = snapshot.CurrentPlayerId;

        IsMyTurn =
            snapshot.CurrentPlayerId == _myPlayerId;

        PendingDrawCards =
            snapshot.PendingDrawCards;

        _drawPileCount =
            snapshot.DrawPileCount;

        _direction =
            snapshot.Direction;

        _hasShoutedUno =
            IsMyTurn && snapshot.HasShoutedUno;

        TopCard = snapshot.TopCard;

        ActiveColorHex = snapshot.ActiveColor switch
        {
            CardColor.Red => "#E74C3C",
            CardColor.Yellow => "#F1C40F",
            CardColor.Blue => "#3498DB",
            CardColor.Green => "#2ECC71",
            _ => "#888888"
        };
        
        var myPlayer = snapshot.Players.FirstOrDefault(
            p => p.Id == _myPlayerId);

        if (myPlayer == null)
            return;

        // Crear la mano privada del jugador.
        PlayerDisplayModel bottom = new()
        {
            Id = myPlayer.Id,
            Name = myPlayer.Name,
            HandCount = snapshot.YourHand.Count
        };

        foreach (Card card in snapshot.YourHand)
        {
            card.IsPlayable = IsMyTurn &&
                (snapshot.PendingDrawCards > 0
                    ? card.Value == CardValue.DrawTwo ||
                      card.Value == CardValue.WildDrawFour
                    : card.CanPlayOn(
                        snapshot.TopCard,
                        snapshot.ActiveColor));

            bottom.Hand.Add(card);
        }

        BottomPlayer = bottom;

        // Ordenar los otros jugadores alrededor del usuario.
        int myIndex = snapshot.Players.FindIndex(
            p => p.Id == _myPlayerId);

        int count = snapshot.Players.Count;

        if (count > 1)
        {
            var left = snapshot.Players[
                (myIndex - 1 + count) % count];

            LeftPlayer = new PlayerDisplayModel
            {
                Id = left.Id,
                Name = left.Name,
                HandCount = left.HandCount
            };
        }

        if (count > 2)
        {
            var right = snapshot.Players[
                (myIndex + 1) % count];

            RightPlayer = new PlayerDisplayModel
            {
                Id = right.Id,
                Name = right.Name,
                HandCount = right.HandCount
            };
        }
        else
        {
            RightPlayer = null;
        }

        var currentPlayer = snapshot.Players.FirstOrDefault(
            p => p.Id == snapshot.CurrentPlayerId);

        StatusMessage = currentPlayer == null
            ? "Partida en curso."
            : IsMyTurn
                ? "¡Es tu turno!"
                : $"Turno de {currentPlayer.Name}.";

        IsWaitingRoomVisible = false;
        IsGameStarted = true;

        if (snapshot.IsFinished)
        {
            IsGameOver = true;

            WinnerName = snapshot.WinnerName ?? "Desconocido";

            GameOverDetails =
                $"La partida {snapshot.PartidaId} ha terminado.";
        }
        else
        {
            IsGameOver = false;
        }

        NotifyBoardProperties();
    }

    // Jugar carta

    private async Task PlayCardAsync(Card card)
    {
        if (card.Color == CardColor.Wild)
        {
            _pendingWildCard = card;
            IsColorPickerVisible = true;
            return;
        }

        await SendPlayCardAsync(card, null);
    }

    private async Task SendPlayCardAsync(
        Card card,
        CardColor? chosenColor)
    {
        try
        {
            if (_client == null)
                return;

            await _client.SendMessageAsync(
                "play_card",
                new PlayCardRequest
                {
                    CardId = card.Id,
                    ChosenColor = chosenColor
                });

            IsColorPickerVisible = false;
            _pendingWildCard = null;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    // Seleccionar color

    private async Task SelectColorAsync(string? colorName)
    {
        if (_pendingWildCard == null ||
            string.IsNullOrWhiteSpace(colorName))
        {
            return;
        }

        if (Enum.TryParse<CardColor>(
            colorName,
            true,
            out CardColor selectedColor))
        {
            if (selectedColor == CardColor.Wild)
                return;

            await SendPlayCardAsync(
                _pendingWildCard,
                selectedColor);
        }
    }

    // Enviar acciones

    private async Task SendActionAsync(string action)
    {
        try
        {
            if (_client == null)
                return;

            await _client.SendMessageAsync(
                action,
                new { });
        }
        catch (Exception ex)
        {
            StatusMessage =
                $"No se pudo enviar la acción: {ex.Message}";
        }
    }

    // Actualizar interfaz

    private void NotifyBoardProperties()
    {
        OnPropertyChanged(nameof(BottomPlayerLabel));

        OnPropertyChanged(nameof(HasCardsInDeck));

        OnPropertyChanged(nameof(CanDrawCard));

        OnPropertyChanged(nameof(CanPassTurn));

        OnPropertyChanged(nameof(HasPendingDraws));

        OnPropertyChanged(nameof(PenaltyButtonText));

        OnPropertyChanged(nameof(CanShoutUno));

        OnPropertyChanged(nameof(ActiveColorHex));

        OnPropertyChanged(nameof(IsMyTurn));
    }

    // Salir del menu

    private async Task ExitToMenuAsync()
    {
        await DisconnectAsync();

        IsGameStarted = false;
        IsGameOver = false;

        IsConnectionScreenVisible = true;
        IsPlayerSelectionVisible = false;
        IsWaitingRoomVisible = false;

        IsColorPickerVisible = false;
        IsPauseMenuVisible = false;

        WinnerName = string.Empty;
        GameOverDetails = string.Empty;

        _myPlayerId = 0;

        StatusMessage = "Introduce la dirección del servidor.";
    }

    private async Task DisconnectAsync()
    {
        if (_receiveCancellation != null)
        {
            _receiveCancellation.Cancel();
            _receiveCancellation.Dispose();
            _receiveCancellation = null;
        }

        _client?.Dispose();
        _client = null;

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch
            {
                
            }

            _receiveTask = null;
        }
    }

    // Modelo del jugador

    public class PlayerDisplayModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int HandCount { get; set; }

        public ObservableCollection<Card> Hand { get; } = new();
    }



    private sealed class AsyncCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;

        private bool _isExecuting;

        public event EventHandler? CanExecuteChanged;

        public AsyncCommand(Func<object?, Task> execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting;
        }

        public async void Execute(object? parameter)
        {
            if (_isExecuting)
                return;

            _isExecuting = true;

            CanExecuteChanged?.Invoke(this, EventArgs.Empty);

            try
            {
                await _execute(parameter);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                _isExecuting = false;

                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action<object?> execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }
}