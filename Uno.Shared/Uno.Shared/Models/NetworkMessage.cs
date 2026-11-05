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
        if (Data.ValueKind == JsonValueKind.Undefined ||
            Data.ValueKind == JsonValueKind.Null)
        {
            return default;
        }

        return Data.Deserialize<T>(SerializerOptions);
    }
}

//Solicitud de clientes

// Primer mensaje que envía un cliente
public class JoinRequest
{
    public string Name { get; set; } = string.Empty;
}

// Solicitud para jugar una carta.
public class PlayCardRequest
{
    public Guid CardId { get; set; }

    public CardColor? ChosenColor { get; set; }
}

//Respuesta del servidor

public class JoinResult
{
    public bool Accepted { get; set; }

    public string Message { get; set; } = string.Empty;

    public int? PlayerId { get; set; }

    public int ConnectedPlayers { get; set; }

    public int RequiredPlayers { get; set; }

    public bool GameStarted { get; set; }
}

public class ActionResult
{
    public bool Accepted { get; set; }

    public string Message { get; set; } = string.Empty;
}


// Información pública de cada jugador.
public class PlayerPublicState
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int HandCount { get; set; }
}

// ESTADO DE LA PARTIDA

public class GameStateSnapshot
{
    public int PartidaId { get; set; }

    public int CurrentPlayerId { get; set; }

    public Card TopCard { get; set; } = null!;

    public CardColor ActiveColor { get; set; }

    public int PendingDrawCards { get; set; }

    public int DrawPileCount { get; set; }

    public bool HasShoutedUno { get; set; }

    public bool IsFinished { get; set; }

    public int? WinnerId { get; set; }

    public string? WinnerName { get; set; }

    public List<PlayerPublicState> Players { get; set; } = new();
    
    public List<Card> YourHand { get; set; } = new();
}