using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Uno.Models;

// Estos tres tipos son el "molde" del JSON que devuelve GET /partida/{id}/estado
// (y todas las acciones POST). Los nombres de [JsonPropertyName] deben coincidir con game_engine.py.

public class CartaDto
{
    [JsonPropertyName("color")] public string Color { get; set; } = string.Empty;
    [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
}

public class JugadorEstadoDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = string.Empty;
    [JsonPropertyName("posicion")] public int Posicion { get; set; }   // asiento real en la mesa (orden de ingreso)
    [JsonPropertyName("cartas")] public int Cartas { get; set; }       // solo la cantidad, nunca las cartas ajenas
    [JsonPropertyName("es_turno")] public bool EsTurno { get; set; }
}

public class EstadoPartidaDto
{
    [JsonPropertyName("sin_cambios")] public bool SinCambios { get; set; }
    [JsonPropertyName("partida_id")] public int PartidaId { get; set; }
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("estado")] public string Estado { get; set; } = string.Empty;   // esperando | en_juego | terminada
    [JsonPropertyName("host_id")] public int HostId { get; set; }
    [JsonPropertyName("jugadores")] public List<JugadorEstadoDto> Jugadores { get; set; } = new();
    [JsonPropertyName("mi_mano")] public List<CartaDto> MiMano { get; set; } = new();
    [JsonPropertyName("top_card")] public CartaDto? TopCard { get; set; }
    [JsonPropertyName("color_actual")] public string ColorActual { get; set; } = "Red";
    [JsonPropertyName("turno_actual_jugador_id")] public int? TurnoActualJugadorId { get; set; }
    [JsonPropertyName("direccion")] public int Direccion { get; set; } = 1;
    [JsonPropertyName("pendientes")] public int Pendientes { get; set; }
    [JsonPropertyName("cartas_en_mazo")] public int CartasEnMazo { get; set; }
    [JsonPropertyName("dijo_uno")] public bool DijoUno { get; set; }
    [JsonPropertyName("ganador_id")] public int? GanadorId { get; set; }
    [JsonPropertyName("ganador_nombre")] public string GanadorNombre { get; set; } = string.Empty;
    [JsonPropertyName("detalle_final")] public string DetalleFinal { get; set; } = string.Empty;
    [JsonPropertyName("ultimo_evento")] public string UltimoEvento { get; set; } = string.Empty;
}
