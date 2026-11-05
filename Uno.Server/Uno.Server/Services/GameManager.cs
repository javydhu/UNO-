using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Uno.Server.Models;
using Uno.Server.Networking;

using Uno.Shared.Models;

namespace Uno.Server.Services;

public class GameManager
{
    private readonly List<ConnectedClient> _clients = new();

    private readonly PythonApiService _pythonApi;

    private readonly GameService _gameService;

    private readonly SemaphoreSlim _gameLock = new(1, 1);

    private readonly int _playersToStart;

    private UnoGame? _game;

    public GameManager(
        PythonApiService pythonApi,
        int playersToStart = 3)
    {
        if (playersToStart < 2 || playersToStart > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(playersToStart));
        }

        _pythonApi = pythonApi;

        _gameService = new GameService(pythonApi);

        _playersToStart = playersToStart;
    }

    // Atender cliente

    public async Task HandleClientAsync(
        ClientConnection connection,
        CancellationToken cancellationToken)
    {
        ConnectedClient? connectedClient = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                NetworkMessage? message =
                    await connection.ReadMessageAsync(
                        cancellationToken);

                if (message == null)
                    break;

                switch (message.Type)
                {
                    // Consultar jugadores

                    case "list_players":

                        if (connectedClient != null)
                        {
                            await SendErrorAsync(
                                connection,
                                "Ya estás dentro de la sala.");

                            break;
                        }

                        await SendRegisteredPlayersAsync(
                            connection);

                        break;

                    // Entrar a la sala

                    case "join":

                        if (connectedClient != null)
                        {
                            await SendErrorAsync(
                                connection,
                                "Ya estás registrado en la sala.");

                            break;
                        }

                        JoinRequest? joinData =
                            message.GetData<JoinRequest>();

                        if (joinData == null)
                        {
                            await SendErrorAsync(
                                connection,
                                "Solicitud de entrada inválida.");

                            break;
                        }

                        connectedClient = await JoinRoomAsync(
                            connection,
                            joinData,
                            cancellationToken);

                        break;

                    // Cancelar entrada

                    case "leave_room":

                        if (connectedClient == null)
                        {
                            await connection.SendMessageAsync(
                                "leave_room_result",
                                new ActionResultMessage
                                {
                                    Accepted = false,
                                    Message = "No estás en una sala."
                                });

                            break;
                        }

                        connectedClient = await LeaveRoomAsync(
                            connectedClient,
                            cancellationToken);

                        break;

                    // Acciones del juego

                    default:

                        if (connectedClient == null)
                        {
                            await SendErrorAsync(
                                connection,
                                "Primero debes entrar a una sala.");

                            break;
                        }

                        await ProcessGameActionAsync(
                            connectedClient,
                            message,
                            cancellationToken);

                        break;
                }
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"JSON inválido: {ex.Message}");
        }
        catch (IOException ex)
        {
            Console.WriteLine(
                $"Error de conexión: {ex.Message}");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Cierre normal del servidor
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Error atendiendo cliente: {ex}");
        }
        finally
        {
            if (connectedClient != null)
            {
                await HandleDisconnectAsync(connectedClient);
            }
        }
    }

    // Consultador jugadores de python

    private async Task SendRegisteredPlayersAsync(
        ClientConnection connection)
    {
        try
        {
            List<RegisteredPlayerDto> players =
                await _pythonApi.ObtenerJugadoresAsync();

            await connection.SendMessageAsync(
                "players_list",
                players);
        }
        catch (Exception ex)
        {
            await SendErrorAsync(
                connection,
                $"No se pudieron consultar los jugadores: {ex.Message}");
        }
    }

    // Entrar a la sala

    private async Task<ConnectedClient?> JoinRoomAsync(
        ClientConnection connection,
        JoinRequest request,
        CancellationToken cancellationToken)
    {
        await _gameLock.WaitAsync(cancellationToken);

        try
        {
            if (_game != null)
            {
                await connection.SendMessageAsync(
                    "join_result",
                    new JoinResult
                    {
                        Accepted = false,
                        Message = "La partida ya comenzó.",
                        RequiredPlayers = _playersToStart
                    });

                return null;
            }

            if (_clients.Count >= _playersToStart)
            {
                await connection.SendMessageAsync(
                    "join_result",
                    new JoinResult
                    {
                        Accepted = false,
                        Message = "La sala está completa.",
                        RequiredPlayers = _playersToStart
                    });

                return null;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                await connection.SendMessageAsync(
                    "join_result",
                    new JoinResult
                    {
                        Accepted = false,
                        Message = "El nombre no es válido."
                    });

                return null;
            }

            // Recuperar o registrar al jugador en Python.
            RegisteredPlayerDto registeredPlayer =
                await _pythonApi.ObtenerOCrearJugadorAsync(
                    request.Name);

            // Evitar que un jugador tenga dos conexiones
            bool alreadyConnected = _clients.Any(
                c => c.Player.Id == registeredPlayer.Id);

            if (alreadyConnected)
            {
                await connection.SendMessageAsync(
                    "join_result",
                    new JoinResult
                    {
                        Accepted = false,
                        Message = "Ese jugador ya está en la sala."
                    });

                return null;
            }

            Player player = new Player(
                registeredPlayer.Id,
                registeredPlayer.Nombre);

            ConnectedClient newClient = new ConnectedClient(
                connection,
                player);

            _clients.Add(newClient);

            bool gameStarted = false;

            // Comprobar que la sala esta completa

            if (_clients.Count == _playersToStart)
            {
                try
                {
                    List<int> playerIds = _clients
                        .Select(c => c.Player.Id)
                        .ToList();

                    // Python crea la partida y registra
                    // a todos los participantes.
                    int partidaId =
                        await _pythonApi.CrearPartidaAsync(playerIds);

                    // Crear el juego.
                    _game = new UnoGame(
                        _clients.Select(c => c.Player).ToList(),
                        partidaId);

                    // El servidor inicia la partida.
                    _game.StartGame();

                    gameStarted = true;

                    Console.WriteLine(
                        $"Partida {partidaId} iniciada.");

                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"No se pudo iniciar la partida: {ex.Message}");

                    _clients.Remove(newClient);

                    await connection.SendMessageAsync(
                        "join_result",
                        new JoinResult
                        {
                            Accepted = false,
                            Message = "No se pudo iniciar la partida. Intenta nuevamente.",
                            ConnectedPlayers = _clients.Count,
                            RequiredPlayers = _playersToStart
                        });

                    await BroadcastWaitingRoomLockedAsync();

                    return null;
                }
            }

            // Informar al jugador que entró.
            await connection.SendMessageAsync(
                "join_result",
                new JoinResult
                {
                    Accepted = true,

                    Message = gameStarted
                        ? "La partida comenzó."
                        : "Entraste a la sala. Esperando jugadores.",

                    PlayerId = player.Id,

                    ConnectedPlayers = _clients.Count,

                    RequiredPlayers = _playersToStart,

                    GameStarted = gameStarted
                });

            Console.WriteLine(
                $"{player.Name} entró a la sala " +
                $"({_clients.Count}/{_playersToStart}).");

            if (gameStarted)
            {
                await BroadcastGameStateLockedAsync();
            }
            else
            {
                await BroadcastWaitingRoomLockedAsync();
            }

            return newClient;
        }
        finally
        {
            _gameLock.Release();
        }
    }

    //Cancelar entrada a la sala

    private async Task<ConnectedClient?> LeaveRoomAsync(
        ConnectedClient client,
        CancellationToken cancellationToken)
    {
        await _gameLock.WaitAsync(cancellationToken);

        try
        {
            // No se puede salir con leaveroom si se esta en la partida
            if (_game != null)
            {
                await client.Connection.SendMessageAsync(
                    "leave_room_result",
                    new ActionResultMessage
                    {
                        Accepted = false,
                        Message = "La partida ya comenzó."
                    });

                return client;
            }

            _clients.Remove(client);

            client.IsConnected = false;

            await client.Connection.SendMessageAsync(
                "leave_room_result",
                new ActionResultMessage
                {
                    Accepted = true,
                    Message = "Saliste de la sala de espera."
                });

            Console.WriteLine(
                $"{client.Player.Name} salió de la sala.");

            await BroadcastWaitingRoomLockedAsync();
            return null;
        }
        finally
        {
            _gameLock.Release();
        }
    }

    // Procesar acciones

    private async Task ProcessGameActionAsync(
        ConnectedClient client,
        NetworkMessage message,
        CancellationToken cancellationToken)
    {
        await _gameLock.WaitAsync(cancellationToken);

        try
        {
            if (_game == null || !_game.IsStarted)
            {
                await client.Connection.SendMessageAsync(
                    "action_result",
                    new ActionResultMessage
                    {
                        Accepted = false,
                        Message = "La partida todavía no comenzó."
                    });

                return;
            }

            ActionResultMessage result =
                await _gameService.ProcessActionAsync(
                    _game,
                    client.Player,
                    message);

            await client.Connection.SendMessageAsync(
                "action_result",
                result);

            if (result.Accepted)
            {
                Console.WriteLine(
                    $"{client.Player.Name}: {message.Type} aceptada.");

                await BroadcastGameStateLockedAsync();
            }
        }
        finally
        {
            _gameLock.Release();
        }
    }

    // Broadcast de la sala de espera

    private async Task BroadcastWaitingRoomLockedAsync()
    {
        WaitingRoomSnapshot snapshot = new()
        {
            ConnectedPlayers = _clients.Count,
            RequiredPlayers = _playersToStart,

            Players = _clients.Select(c =>
                new PlayerPublicState
                {
                    Id = c.Player.Id,
                    Name = c.Player.Name,
                    HandCount = 0,
                    IsConnected = c.IsConnected
                }).ToList()
        };

        foreach (ConnectedClient client in _clients.ToList())
        {
            if (!client.IsConnected)
                continue;

            try
            {
                await client.Connection.SendMessageAsync(
                    "waiting_room_update",
                    snapshot);
            }
            catch (IOException ex)
            {
                Console.WriteLine(
                    $"Error actualizando sala: {ex.Message}");

                client.IsConnected = false;
            }
        }
    }
//Broadcast del juego

    private async Task BroadcastGameStateLockedAsync()
    {
        if (_game == null)
            return;

        foreach (ConnectedClient recipient in _clients)
        {
            if (!recipient.IsConnected)
                continue;

            GameStateSnapshot snapshot =
                CreateSnapshot(recipient.Player);

            try
            {
                await recipient.Connection.SendMessageAsync(
                    "game_state",
                    snapshot);
            }
            catch (IOException ex)
            {
                recipient.IsConnected = false;

                Console.WriteLine(
                    $"Error enviando a {recipient.Player.Name}: {ex.Message}");
            }
        }
    }

    // Crear estado privado por jugador

    private GameStateSnapshot CreateSnapshot(Player viewer)
    {
        if (_game == null)
            throw new InvalidOperationException(
                "No existe una partida.");

        return new GameStateSnapshot
        {
            PartidaId = _game.PartidaId,

            CurrentPlayerId = _game.CurrentPlayer.Id,

            Direction = _game.Direction,

            TopCard = _game.TopCard,

            ActiveColor = _game.ActiveColor,

            PendingDrawCards = _game.PendingDrawCards,

            ConsecutivePasses = _game.ConsecutivePasses,

            DrawPileCount = _game.DrawPile.Count,

            HasShoutedUno = _game.HasShoutedUno,

            IsFinished = _game.IsFinished,

            WinnerId = _game.Winner?.Id,

            WinnerName = _game.Winner?.Name,

            Players = _game.Players.Select(player =>
                new PlayerPublicState
                {
                    Id = player.Id,
                    Name = player.Name,
                    HandCount = player.Hand.Count,
                    IsConnected = _clients.Any(
                        c => c.Player.Id == player.Id && c.IsConnected)
                }).ToList(),

            // Únicamente las cartas del destinatario.
            YourHand = viewer.Hand.ToList()
        };
    }

    // Desconectar cliente

    private async Task HandleDisconnectAsync(
        ConnectedClient client)
    {
        await _gameLock.WaitAsync();

        try
        {
            client.IsConnected = false;

            if (_game == null)
            {
                
                _clients.Remove(client);

                await BroadcastWaitingRoomLockedAsync();
            }
            else
            {
                
                Console.WriteLine(
                    $"{client.Player.Name} se desconectó durante la partida.");

                await BroadcastGameStateLockedAsync();
            }
        }
        finally
        {
            _gameLock.Release();
        }
    }
    

    private async Task SendErrorAsync(
        ClientConnection connection,
        string message)
    {
        try
        {
            await connection.SendMessageAsync(
                "server_error",
                new ActionResultMessage
                {
                    Accepted = false,
                    Message = message
                });
        }
        catch
        {
        }
    }
}