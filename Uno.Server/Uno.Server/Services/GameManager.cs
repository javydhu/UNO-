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
    // Clientes conectados a la sala
    private readonly List<ConnectedClient> _clients = new();

    private UnoGame? _game;

    private readonly GameService _gameService = new();

    // Evita modificaciones simultáneas de una partida.
    private readonly SemaphoreSlim _gameLock = new(1, 1);

    private readonly int _playersToStart;

    private int _nextPlayerId = 1;

    private int _nextGameId = 1;

    public GameManager(int playersToStart = 3)
    {
        if (playersToStart < 2 || playersToStart > 10)
            throw new ArgumentOutOfRangeException(
                nameof(playersToStart));

        _playersToStart = playersToStart;
    }

    
    // Administracion de conexiones
    public async Task HandleClientAsync(
        ClientConnection connection,
        CancellationToken cancellationToken)
    {
        ConnectedClient? connectedClient = null;

        try
        {
            // Primer mensaje de unirse
            NetworkMessage? firstMessage =
                await connection.ReadMessageAsync();

            if (firstMessage == null ||
                firstMessage.Type != "join")
            {
                await connection.SendMessageAsync(
                    "join_result",
                    new JoinResult
                    {
                        Accepted = false,
                        Message = "Debes identificarte primero."
                    });

                return;
            }

            JoinRequest? joinRequest =
                firstMessage.GetData<JoinRequest>();

            if (joinRequest == null ||
                string.IsNullOrWhiteSpace(joinRequest.Name))
            {
                await connection.SendMessageAsync(
                    "join_result",
                    new JoinResult
                    {
                        Accepted = false,
                        Message = "El nombre del jugador no es válido."
                    });

                return;
            }

            JoinResult joinResult;

            // Registrar a jugador

            await _gameLock.WaitAsync(cancellationToken);

            try
            {
                if (_game != null ||
                    _clients.Count >= _playersToStart)
                {
                    joinResult = new JoinResult
                    {
                        Accepted = false,
                        Message = "La sala ya está completa."
                    };
                }
                else
                {
                    Player player = new Player(
                        _nextPlayerId++,
                        joinRequest.Name.Trim());

                    connectedClient = new ConnectedClient(
                        connection,
                        player);

                    _clients.Add(connectedClient);

                    Console.WriteLine(
                        $"Jugador conectado: {player.Name} " +
                        $"(ID: {player.Id})");

                    // Comprobacion de jugadores suficientes
                    bool gameStarted = false;

                    if (_clients.Count == _playersToStart)
                    {
                        StartNewGame();

                        gameStarted = true;
                    }

                    joinResult = new JoinResult
                    {
                        Accepted = true,
                        Message = gameStarted
                            ? "La partida ha comenzado."
                            : "Esperando a más jugadores.",

                        PlayerId = player.Id,

                        ConnectedPlayers = _clients.Count,

                        RequiredPlayers = _playersToStart,

                        GameStarted = gameStarted
                    };
                }
            }
            finally
            {
                _gameLock.Release();
            }

            // Responder al jugador que hizo conexion
            await connection.SendMessageAsync(
                "join_result",
                joinResult);

            if (!joinResult.Accepted)
                return;

            // Actualizar a los clientes
            await _gameLock.WaitAsync(cancellationToken);

            try
            {
                if (_game != null && _game.IsStarted)
                {
                    await BroadcastGameStateLockedAsync();
                }
                else
                {
                    await BroadcastWaitingLockedAsync();
                }
            }
            finally
            {
                _gameLock.Release();
            }

            // Escuchar solicitudes

            while (!cancellationToken.IsCancellationRequested)
            {
                NetworkMessage? message =
                    await connection.ReadMessageAsync();

                // se hace null si ya no existe la conexion
                if (message == null)
                    break;

                await ProcessClientMessageAsync(
                    connectedClient,
                    message,
                    cancellationToken);
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"Mensaje JSON inválido: {ex.Message}");
        }
        catch (IOException ex)
        {
            Console.WriteLine(
                $"Error de conexión: {ex.Message}");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Error atendiendo cliente: {ex.Message}");
        }
        finally
        {
            if (connectedClient != null)
            {
                await DisconnectClientAsync(connectedClient);
            }
        }
    }

    // Crear e iniciar partida

    private void StartNewGame()
    {
        List<Player> players = _clients
            .Select(client => client.Player)
            .ToList();

        _game = new UnoGame(
            players,
            _nextGameId++);

        _game.StartGame();

        Console.WriteLine(
            $"Partida {_game.PartidaId} iniciada.");

        Console.WriteLine(
            $"Jugadores: {_game.Players.Count}");

        Console.WriteLine(
            $"Primer turno: {_game.CurrentPlayer.Name}");
    }

    // Procesar las solicitudes

    private async Task ProcessClientMessageAsync(
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
                    new ActionResult
                    {
                        Accepted = false,
                        Message = "La partida todavía no ha comenzado."
                    });

                return;
            }

            ActionResult result = _gameService.ProcessAction(
                _game,
                client.Player,
                message);

            // Responder al jugador que realizó la acción.
            await client.Connection.SendMessageAsync(
                "action_result",
                result);

            Console.WriteLine(
                $"{client.Player.Name}: {message.Type} " +
                $"| Aceptada: {result.Accepted}");

            // Si la accion es valida, actualizar a todos.
            if (result.Accepted)
            {
                await BroadcastGameStateLockedAsync();
            }
        }
        finally
        {
            _gameLock.Release();
        }
    }

    
    // Broadcast del estado de la partida

    private async Task BroadcastGameStateLockedAsync()
    {
        if (_game == null)
            return;

        foreach (ConnectedClient recipient in _clients)
        {
            if (!recipient.IsConnected)
                continue;

            // Construir un estado para cada jugador.
            GameStateSnapshot snapshot = CreateSnapshot(
                recipient.Player);

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
                    $"No se pudo enviar el estado a " +
                    $"{recipient.Player.Name}: {ex.Message}");
            }
        }
    }

    
    // Creacion del estado personalizado
    

    private GameStateSnapshot CreateSnapshot(Player viewer)
    {
        if (_game == null)
            throw new InvalidOperationException(
                "No existe una partida activa.");

        return new GameStateSnapshot
        {
            PartidaId = _game.PartidaId,

            CurrentPlayerId = _game.CurrentPlayer.Id,

            TopCard = _game.TopCard,

            ActiveColor = _game.ActiveColor,

            PendingDrawCards = _game.PendingDrawCards,

            DrawPileCount = _game.DrawPile.Count,

            HasShoutedUno = _game.HasShoutedUno,

            IsFinished = _game.IsFinished,

            WinnerId = _game.Winner?.Id,

            WinnerName = _game.Winner?.Name,

            Players = _game.Players
                .Select(player => new PlayerPublicState
                {
                    Id = player.Id,

                    Name = player.Name,

                    HandCount = player.Hand.Count
                })
                .ToList(),

            // Los clientes solo tienen sus propias manos
            YourHand = viewer.Hand.ToList()
        };
    }

    // Informar a los jugadores que estan en espera

    private async Task BroadcastWaitingLockedAsync()
    {
        foreach (ConnectedClient client in _clients)
        {
            if (!client.IsConnected)
                continue;

            try
            {
                await client.Connection.SendMessageAsync(
                    "waiting_for_players",
                    new
                    {
                        ConnectedPlayers = _clients.Count,
                        RequiredPlayers = _playersToStart
                    });
            }
            catch (IOException)
            {
                client.IsConnected = false;
            }
        }
    }

    // Desconectar al jugador

    private async Task DisconnectClientAsync(
        ConnectedClient client)
    {
        await _gameLock.WaitAsync();

        try
        {
            client.IsConnected = false;

            Console.WriteLine(
                $"Jugador desconectado: {client.Player.Name}");

            // Si todavía no comenzó la partida, se libera espacio
            if (_game == null)
            {
                _clients.Remove(client);

                await BroadcastWaitingLockedAsync();
            }

            // Si la partida ya comenzó, se conserva la mano
        }
        finally
        {
            _gameLock.Release();
        }
    }
}