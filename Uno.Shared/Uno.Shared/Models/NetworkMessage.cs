using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Uno.Shared.Models;

public class NetworkMessage
{
    public string Type { get; set; } = string.Empty;

    public JsonElement Data { get; set; }

    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static NetworkMessage Create<T>(string type, T data)
    {
        return new NetworkMessage
        {
            Type = type,
            Data = JsonSerializer.SerializeToElement(
                data,
                SerializerOptions)
        };
    }

    public T? GetData<T>()
    {
        if (Data.ValueKind is JsonValueKind.Undefined
            or JsonValueKind.Null)
        {
            return default;
        }

        return Data.Deserialize<T>(SerializerOptions);
    }
}

public class RegisteredPlayerDto
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public int PartidasGanadas { get; set; }
}

public class JoinRequest
{
    public string Name { get; set; } = string.Empty;
}

public class PlayCardRequest
{
    public Guid CardId { get; set; }

    public CardColor? ChosenColor { get; set; }
}

public class JoinResult
{
    public bool Accepted { get; set; }

    public string Message { get; set; } = string.Empty;

    public int? PlayerId { get; set; }

    public int ConnectedPlayers { get; set; }

    public int RequiredPlayers { get; set; }

    public bool GameStarted { get; set; }
}

public class ActionResultMessage
{
    public bool Accepted { get; set; }

    public string Message { get; set; } = string.Empty;
}


public class WaitingRoomSnapshot
{
    public int ConnectedPlayers { get; set; }

    public int RequiredPlayers { get; set; }

    public List<PlayerPublicState> Players { get; set; } = new();
}

public class PlayerPublicState
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int HandCount { get; set; }

    public bool IsConnected { get; set; } = true;
}
public class GameStateSnapshot
{
    public int PartidaId { get; set; }

    public int CurrentPlayerId { get; set; }

    public int Direction { get; set; }

    public Card TopCard { get; set; } = null!;

    public CardColor ActiveColor { get; set; }

    public int PendingDrawCards { get; set; }

    public int ConsecutivePasses { get; set; }

    public int DrawPileCount { get; set; }

    public bool HasShoutedUno { get; set; }

    public bool IsFinished { get; set; }

    public int? WinnerId { get; set; }

    public string? WinnerName { get; set; }

    public List<PlayerPublicState> Players { get; set; } = new();
    public List<Card> YourHand { get; set; } = new();
}