namespace Uno.Models;

// Una fila de la sala de espera: un jugador ya sentado o un lugar libre. Solo para dibujar.
public class LobbyJugador
{
    public string Nombre { get; set; } = string.Empty;
    public string Inicial { get; set; } = "?";
    public string ColorHex { get; set; } = "#FFFFFF";
    public bool EsAnfitrion { get; set; }
    public bool EsYo { get; set; }
    public bool EsVacio { get; set; }
}
