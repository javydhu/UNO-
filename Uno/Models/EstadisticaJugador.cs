namespace Uno.Models;

// Una fila de las estadisticas: sirve tanto para una barra de la grafica como para una fila de la tabla. Solo para dibujar.
public class EstadisticaJugador
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#FFFFFF";
    public int Puntos { get; set; }
    public int PartidasGanadas { get; set; }
    public double AlturaBarra { get; set; }     // alto en pixeles de la barra, proporcional a las partidas ganadas
    public bool EsGanador { get; set; }         // gano la partida que acaba de terminar
    public bool EsYo { get; set; }
}
