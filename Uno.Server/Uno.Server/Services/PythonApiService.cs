using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Uno.Shared.Models;

namespace Uno.Server.Services;

public class PythonApiService
{
    private readonly HttpClient _httpClient;

    public PythonApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        if (_httpClient.BaseAddress == null)
        {
            throw new ArgumentException(
                "No se configuró la dirección de Python API.");
        }
    }

    private async Task<string> SendRequestAsync(
        HttpMethod method,
        string endpoint,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request =
            new(method, endpoint);

        if (data != null)
        {
            request.Content = JsonContent.Create(data);
        }

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        string content =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Python API respondió {(int)response.StatusCode}: {content}");
        }

        // Detectar errores enviados como JSON.
        try
        {
            using JsonDocument document =
                JsonDocument.Parse(content);

            if (document.RootElement.ValueKind ==
                JsonValueKind.Object)
            {
                if (document.RootElement.TryGetProperty(
                    "error", out JsonElement error))
                {
                    throw new InvalidOperationException(
                        error.ToString());
                }
            }
        }
        catch (JsonException)
        {
            // Las respuestas no JSON se validan
            // en el método que las consume.
        }

        return content;
    }
    
    public async Task<List<RegisteredPlayerDto>>
        ObtenerJugadoresAsync()
    {
        string response = await SendRequestAsync(
            HttpMethod.Get,
            "jugadores/");

        return JsonSerializer.Deserialize<List<RegisteredPlayerDto>>(
            response,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<RegisteredPlayerDto>();
    }

    public async Task<RegisteredPlayerDto>
        ObtenerOCrearJugadorAsync(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException(
                "El nombre no puede estar vacío.");
        }

        string response = await SendRequestAsync(
            HttpMethod.Post,
            "jugadores/login",
            new { nombre = nombre.Trim() });

        RegisteredPlayerDto? player =
            JsonSerializer.Deserialize<RegisteredPlayerDto>(
                response,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (player == null || player.Id <= 0)
        {
            throw new InvalidOperationException(
                "Python no devolvió un jugador válido.");
        }

        return player;
    }

    public async Task<int> CrearPartidaAsync(
        List<int> jugadoresIds)
    {
        if (jugadoresIds.Count < 2)
        {
            throw new ArgumentException(
                "Se necesitan al menos dos jugadores.");
        }

        string response = await SendRequestAsync(
            HttpMethod.Post,
            "partida/nueva",
            new
            {
                jugadores_ids = jugadoresIds
            });

        using JsonDocument document =
            JsonDocument.Parse(response);

        int partidaId = document.RootElement
            .GetProperty("partida_id")
            .GetInt32();

        if (partidaId <= 0)
        {
            throw new InvalidOperationException(
                "El identificador de partida no es válido.");
        }

        return partidaId;
    }

    // Se conserva por si necesitamos asociar un jugador a la partida existente
    public async Task UnirJugadorAsync(
        int jugadorId,
        int partidaId)
    {
        await SendRequestAsync(
            HttpMethod.Post,
            "partida/unirse",
            new
            {
                jugador_id = jugadorId,
                partida_id = partidaId
            });
    }

    public async Task RegistrarJugadaEnServidorAsync(
        Player jugador,
        int partidaId,
        Card carta,
        CardColor? colorElegido = null)
    {
        await SendRequestAsync(
            HttpMethod.Post,
            "partida/jugar-carta",
            new
            {
                jugador_id = jugador.Id,
                partida_id = partidaId,
                color_carta = carta.Color.ToString(),
                valor_carta = carta.Value.ToString(),
                color_elegido = colorElegido?.ToString()
            });
    }
    

    public async Task RegistrarVictoriaEnServidorAsync(
        Player jugador,
        int partidaId)
    {
        await SendRequestAsync(
            HttpMethod.Post,
            "partida/gano",
            new
            {
                jugador_id = jugador.Id,
                partida_id = partidaId
            });
    }
    

    public async Task RegistrarRoboEnServidorAsync(
        Player jugador,
        int partidaId,
        Card carta)
    {
        await SendRequestAsync(
            HttpMethod.Post,
            "partida/robar-carta",
            new
            {
                jugador_id = jugador.Id,
                partida_id = partidaId,
                color_carta = carta.Color.ToString(),
                valor_carta = carta.Value.ToString()
            });
    }

    public async Task RegistrarPenalizacionEnServidorAsync(
        Player jugador,
        int partidaId)
    {
        await SendRequestAsync(
            HttpMethod.Post,
            "partida/tomar-penalizacion",
            new
            {
                jugador_id = jugador.Id,
                partida_id = partidaId
            });
    }
    

    public async Task RegistrarPaseDeTurnoEnServidorAsync(
        Player jugador,
        int partidaId)
    {
        await SendRequestAsync(
            HttpMethod.Post,
            "partida/pasar-turno",
            new
            {
                jugador_id = jugador.Id,
                partida_id = partidaId
            });
    }

    public async Task RegistrarUnoAsync(
        Player jugador,
        int partidaId,
        bool decision)
    {
        await SendRequestAsync(
            HttpMethod.Post,
            "partida/uno",
            new
            {
                jugador_id = jugador.Id,
                partida_id = partidaId,
                decision
            });
    }
    

    public async Task RegistrarMazoAgotadoAsync(
        Player jugador,
        int partidaId)
    {
        await SendRequestAsync(
            HttpMethod.Post,
            "partida/sin-cartas",
            new
            {
                jugador_id = jugador.Id,
                partida_id = partidaId
            });
    }
}