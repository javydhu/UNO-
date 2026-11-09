using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Uno.Models;

// Molde del JSON que devuelve GET /partida/{id}/estadisticas (ver obtener_estadisticas en main.py).

public class EstadisticaJugadorDto
{
    [JsonPropertyName("id")] public int Id { get; set; }                                // ID del jugador en MySQL
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = string.Empty;
    [JsonPropertyName("posicion")] public int Posicion { get; set; }                    // asiento en la mesa
    [JsonPropertyName("puntos")] public int Puntos { get; set; }                        // valor de las cartas que le quedaron en la mano
    [JsonPropertyName("partidas_ganadas")] public int PartidasGanadas { get; set; }     // acumuladas en MySQL
}

public class EstadisticasPartidaDto
{
    [JsonPropertyName("partida_id")] public int PartidaId { get; set; }
    [JsonPropertyName("ganador_id")] public int? GanadorId { get; set; }
    [JsonPropertyName("jugadores")] public List<EstadisticaJugadorDto> Jugadores { get; set; } = new();
}
