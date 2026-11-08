using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Uno.Models;
using Uno.Services;
using System.Collections.Generic;

namespace Uno.ViewModels;

public class MainViewModel : ViewModelBase
{
    private UnoGame _game;
    private string _estatusMensaje = string.Empty;
    private bool _JuegoIniciado = false;
    private bool _ColorElegidoVisible = false;
    private bool _JuegoTerminado = false;
    private string _ganadorNombre = string.Empty;
    private string _gameOverDetails = string.Empty;
    private Card? _CartaWildPendiente = null;
    private int partidaID = 0;
    private bool _haDichoUno = false;
    private bool _menuPausaVisible = false;
    private bool _isProcessingPlay = false; 
    private bool _isStartingGame = false;
    private string _nombreUsuario = "Jugador_1";
    private string _ipServidor = "127.0.0.1";
    private string _idPartidaInput = string.Empty;

    public string IDPartidaInput
    {
        get => _idPartidaInput;
        set => SetProperty(ref _idPartidaInput, value);
    }

    public string NombreUsuario
    {
        get => _nombreUsuario;
        set => SetProperty(ref _nombreUsuario, value);
    }

    public string IPServidor
    {
        get => _ipServidor;
        set => SetProperty(ref _ipServidor, value);
    }

    // El botón de UNO aparece solo si el jugador tiene 2 cartas, al menos una es jugable y aún no lo ha presionado
    public bool CanShoutUno => Game.Jugadores.Count > 0
                            && Game.actualJugador.Mano.Count == 2 
                            && Game.actualJugador.Mano.Any(c => c.esJugable) 
                            && !_haDichoUno;
    public class JugadorRespuesta
    {
        public string Nombre { get; set; } = string.Empty;
        public int Id { get; set; }
        [JsonPropertyName("partidas_ganadas")]
        public int PartidasGanadas  { get; set; }
    }

    public Card TopCard => Game.TopCard;

    public UnoGame Game
    {
        get => _game;
        set => SetProperty(ref _game, value);
    }

    public string EstatusMensaje
    {
        get => _estatusMensaje;
        set => SetProperty(ref _estatusMensaje, value);
    }

    public bool JuegoIniciado
    {
        get => _JuegoIniciado;
        set => SetProperty(ref _JuegoIniciado, value);
    }

    public bool ColorElegidoVisible
    {
        get => _ColorElegidoVisible;
        set => SetProperty(ref _ColorElegidoVisible, value);
    }

    public bool JuegoTerminado
    {
        get => _JuegoTerminado;
        set => SetProperty(ref _JuegoTerminado, value);
    }

    public string GanadorNombre
    {
        get => _ganadorNombre;
        set => SetProperty(ref _ganadorNombre, value);
    }

    public string GameOverDetails
    {
        get => _gameOverDetails;
        set => SetProperty(ref _gameOverDetails, value);
    }

    // Propiedades calculadas para la interfaz
    public int CartasPendientes => Game.PilaPendiente;
    public bool TienePendientes => Game.PilaPendiente > 0;
    public bool PuedePasar => Game.Pila.Count == 0 && !TienePendientes;
    private int _mensajeToken = 0; // Ayuda a cancelar mensajes viejos si suceden rápido
    public bool HayCartasEnMazo => Game.Pila.Count > 0;
    public string BotonPenalizacion => Game.Pila.Count == 0 
        ? "Pasar Turno" 
        : $"Tomar +{CartasPendientes} y Perder Turno";
    
    private async void MensajeTemporal(string mensaje, int duracionMs = 5000)
    {
        int currentToken = ++_mensajeToken;
        EstatusMensaje = mensaje;

        await Task.Delay(duracionMs);

        if (currentToken == _mensajeToken)
        {
            ActualizarEstatus(); 
        }
    }

    public string ActiveColorHex => Game.ColorActual switch
    {
        CardColor.Red => "#E74C3C",
        CardColor.Yellow => "#F1C40F",
        CardColor.Blue => "#3498DB",
        CardColor.Green => "#2ECC71",
        _ => "#888888"
    };

    public Player? BottomPlayer => Game.Jugadores.Count == 0 ? null : Game.actualJugador;

    public Player? RightPlayer
    {
        get
        {
            if (Game.Jugadores.Count == 0) return null;
            int nextIdx = (Game.actualJugadorIndex + Game.Direccion + Game.Jugadores.Count) % Game.Jugadores.Count;
            return Game.Jugadores[nextIdx];
        }
    }

    public Player? LeftPlayer
    {
        get
        {
            if (Game.Jugadores.Count == 0) return null;
            int leftIdx = (Game.actualJugadorIndex - Game.Direccion + Game.Jugadores.Count) % Game.Jugadores.Count;
            return Game.Jugadores[leftIdx];
        }
    }

    public MainViewModel()
    {
        _game = new UnoGame();
    }

    public async Task StartGame()
    {
        if (_isStartingGame) return; // Ya se esta creando una partida, se ignora el clic extra
        _isStartingGame = true;

        try
        {
            ApiService.ServerIP = string.IsNullOrWhiteSpace(IPServidor) ? "127.0.0.1" : IPServidor.Trim();

            ApiService api = new ApiService();
            List<Player> listaJugadores = new List<Player>();

            // 1. Registro del jugador en el servidor
            int miId = 1;
            string nombreFinal = string.IsNullOrWhiteSpace(NombreUsuario) ? "Jugador_1" : NombreUsuario.Trim();

            string? respuestaLogin = await api.EnviarJugadaLog("/jugadores/login", new { nombre = nombreFinal });
            if (string.IsNullOrEmpty(respuestaLogin))
            {
                MensajeTemporal("Error: No se pudo conectar a la IP del servidor.", 5000);
                JuegoIniciado = false;
                return;
            }

            try
            {
                var jugadorServidor = JsonSerializer.Deserialize<JugadorRespuesta>(respuestaLogin,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (jugadorServidor != null && jugadorServidor.Id > 0)
                {
                    miId = jugadorServidor.Id;
                }
            }
            catch (JsonException e)
            {
                Console.WriteLine(e.Message);
            }

            // 2. Determinar si CREA una partida nueva o se UNE a una existente
            if (int.TryParse(IDPartidaInput?.Trim(), out int idExistente) && idExistente > 0)
            {
                // UNIRSE A PARTIDA EXISTENTE
                partidaID = idExistente;
                var datosUnirse = new { partida_id = partidaID, jugador_id = miId };
                string? ansUnirse = await api.EnviarJugadaLog("/partida/unirse", datosUnirse);

                if (string.IsNullOrEmpty(ansUnirse))
                {
                    MensajeTemporal($"Error: No existe la partida ID {partidaID} en el servidor.", 5000);
                    JuegoIniciado = false;
                    return;
                }
            }
            else
            {
                // CREAR PARTIDA NUEVA (Host)
                string? ansNueva = await api.EnviarJugadaLog("/partida/nueva", "");
                if (!string.IsNullOrEmpty(ansNueva))
                {
                    try
                    {
                        var datosJson = JsonSerializer.Deserialize<Dictionary<string, int>>(ansNueva);
                        partidaID = datosJson != null && datosJson.ContainsKey("partida_id") ? datosJson["partida_id"] : 1;
                    }
                    catch (JsonException e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }

            // 3. Cargar la lista de jugadores local
            listaJugadores.Add(new Player(miId, nombreFinal));
            listaJugadores.Add(new Player(2, "Jugador_2"));
            listaJugadores.Add(new Player(3, "Jugador_3"));

            _game = new UnoGame(listaJugadores, partidaID);

            // Se limpia el estado que pudo quedar de la partida anterior
            _haDichoUno = false;
            _CartaWildPendiente = null;
            ColorElegidoVisible = false;
            MenuPausaVisible = false;

            JuegoIniciado = true;
            JuegoTerminado = false;

            AssignCardActions();
            ActualizarEstatus();
            NotifyAllPositions();
        }
        finally
        {
            _isStartingGame = false;
        }
    }

    private void AssignCardActions()
    {
        foreach (var player in Game.Jugadores)
        {
            foreach (var card in player.Mano)
            {
                card.PlayAction = JugarCarta;
            }
        }
    }

    public async void JugarCarta(Card card)
    {
        if (JuegoTerminado || _isProcessingPlay) return;

        if (card.Color == CardColor.Wild)
        {
            _CartaWildPendiente = card;
            ColorElegidoVisible = true;
            return;
        }
        ExecutePlayCard(card, null);
    }

    public void SeleccionarColor(object? parameter)
    {
        if (parameter is string colorName && _CartaWildPendiente != null)
        {
            if (Enum.TryParse<CardColor>(colorName, out var chosenColor))
            {
                ColorElegidoVisible = false;
                ExecutePlayCard(_CartaWildPendiente, chosenColor);
                _CartaWildPendiente = null;
            }
        }
    }
    public bool MenuPausaVisible
    {
        get => _menuPausaVisible;
        set => SetProperty(ref _menuPausaVisible, value);
    }
    public void TogglePauseMenu()
    {
        MenuPausaVisible = !MenuPausaVisible;
    }

    // Reinicia la partida actual (cierra el menú y llama a StartGame)
    public async Task RestartGame()
    {
        MenuPausaVisible = false;
        await StartGame();
    }

    // Abandona la partida y vuelve a la pantalla inicial
    public void QuitToMainMenu()
    {
        MenuPausaVisible = false;
        JuegoIniciado = false;
        JuegoTerminado = false;
        // Podrías llamar a un endpoint de la API para avisar que se abandonó la partida
    }

    private async void ExecutePlayCard(Card card, CardColor? chosenColor)
    {
        if (_isProcessingPlay) return;
        // Solo se permite jugar si es el turno de TU jugador local
        if (Game.actualJugador.ID != BottomPlayer?.ID)
        {
            MensajeTemporal("¡No es tu turno!", 3000);
            _isProcessingPlay = false;
            return;
        }

        try
        {
            Player currentPlayer = _game.actualJugador;
            int cardsBeforePlay = currentPlayer.Mano.Count; // Guardamos cuántas cartas tenía antes de jugar
            string? mensajeCastigo = null; // Se muestra al final, porque UpdateStatus() sobreescribe el mensaje de estado

            if (_game.JugarCarta(currentPlayer, card, chosenColor))
            {
                string? colorElegidoStr = chosenColor?.ToString();
                await RegistrarJugadaEnServidor(currentPlayer, card , colorElegidoStr);

                // CASTIGO POR NO DECIR UNO
                if (cardsBeforePlay == 2) // Si tenía 2 cartas y jugó una, se quedó con 1
                {
                    if (!_haDichoUno)
                    {
                        await RegistrarUno(currentPlayer);
                        mensajeCastigo = $"¡{currentPlayer.Name} olvidó decir UNO! Roba 2 cartas de castigo.";
                        for (int i = 0; i < 2; i++)
                        {
                            Card? drawn = _game.TomarCartaDePila();
                            if (drawn != null)
                            {
                                currentPlayer.Mano.Add(drawn);
                                await RegistrarRoboEnServidor(currentPlayer, drawn);
                            }
                        }
                    }
                }
                _haDichoUno = false;

                if (currentPlayer.Mano.Count == 0)
                {
                    TerminarConGanador(currentPlayer.Name, "¡Se ha quedado sin cartas!");
                    await RegistrarVictoriaEnServidor(currentPlayer);
                    return;
                }

                AssignCardActions();
                ActualizarEstatus();
                NotifyAllPositions();

                if (mensajeCastigo != null) MensajeTemporal(mensajeCastigo, 5000);
            }
            else
            {
                string errorMsg = Game.PilaPendiente > 0
                    ? $"¡Hay un +{Game.PilaPendiente} acumulado! Responde con +2/+4 o toma el castigo."
                    : "Movimiento inválido. Elige una carta del mismo color, número o comodín.";
               
                MensajeTemporal(errorMsg, 5000);
            }
        }
        finally
        {
            _isProcessingPlay = false;
        }
    }

    // BOTÓN ¡UNO!
    public async void DecirUno()
    {
        _haDichoUno = true;
        await RegistrarUno(_game.actualJugador);
        MensajeTemporal($"¡{Game.actualJugador.Name} ha dicho UNO!", 3000);
        NotifyAllPositions(); // Refrescamos para que el botón desaparezca
    }
    public async void DrawCard()
    {
        if (JuegoTerminado || Game.Pila.Count == 0 || TienePendientes) return;

        Card? drawnCard = _game.TomarCartaDePila();
        if (drawnCard != null)
        {
            drawnCard.PlayAction = JugarCarta;
            _game.actualJugador.Mano.Add(drawnCard);
           
            await RegistrarRoboEnServidor(_game.actualJugador, drawnCard);
            MensajeTemporal($"{_game.actualJugador.Name} ha robado una carta.", 3000);
        }

        NotifyAllPositions();
    }

    public async void TomarPenalizacion()
    {
        if(JuegoTerminado || _isProcessingPlay) return;
        if (Game.PilaPendiente > 0)
        {
            Player victim = Game.actualJugador;
            int count = Game.PilaPendiente;
            Game.ResuelveTomarPendientes(victim);
            
            AssignCardActions();
            
            _haDichoUno = false; // Reset de UNO si se pierde el turno
                                
            MensajeTemporal($"{victim.Name} no pudo responder y robó {count} cartas.", 5000);
                                           
            NotifyAllPositions();
            
            await RegistrarPenalizacionEnServidor(victim);
        }
    }

    public async void PasarTurno()
    {
        if (JuegoTerminado || _isProcessingPlay || !PuedePasar) return;

        Game.PasesContinuos++;

        if (Game.PasesContinuos >= Game.Jugadores.Count)
        {
            CalcularPuntosFinales();
            return;
        }
        Player jugadorQuePaso = _game.actualJugador;

        _haDichoUno = false; // Reset de UNO

        // Primero se cambia el turno y se refresca la pantalla; hasta después se avisa al servidor.
        // Así un doble clic en "Pasar Turno" ya no avanza el turno dos veces.
        Game.SiguienteTurno();
        AssignCardActions();
        ActualizarEstatus();
        NotifyAllPositions();

        await RegistrarPaseDeTurnoEnServidor(jugadorQuePaso);
    }

    private async void CalcularPuntosFinales()
    {
        var scores = Game.Jugadores.Select(p => new { Player = p, Score = Game.CalcularPuntos(p) })
            .OrderBy(x => x.Score)
            .ToList();

        var winner = scores.First();

        string details = "PUNTUACIÓN FINAL:\n";
        foreach (var s in scores)
        {
            details += $"{s.Player.Name}: {s.Score} pts\n";
        }

        // Primero se muestra la pantalla final (IsGameOver = true) y hasta después se avisa al servidor.
        // Así un doble clic en "Pasar Turno" ya no registra dos veces la victoria.
        TerminarConGanador(winner.Player.Name, details);
        await RegistrarVictoriaEnServidor(winner.Player);
    }


    private void TerminarConGanador(string name, string details)
    {
        GanadorNombre = name;
        GameOverDetails = details;
        JuegoTerminado = true;
        EstatusMensaje = $"¡JUEGO FINALIZADO! Ganador: {GanadorNombre}";
        NotifyAllPositions();
    }
    
    private void ActualizarCartasJugables()
    {
        if (Game == null || Game.Jugadores.Count == 0) return;

        foreach (var card in Game.actualJugador.Mano)
        {
            if (Game.PilaPendiente > 0)
            {
                card.esJugable = (card.Value == CardValue.DrawTwo || card.Value == CardValue.WildDrawFour);
            }
            else
            {
                card.esJugable = card.Jugable(TopCard, Game.ColorActual);
            }
        }
    }

    private void NotifyAllPositions()
    {
        ActualizarCartasJugables();
        OnPropertyChanged(nameof(Game));
        OnPropertyChanged(nameof(BottomPlayer));
        OnPropertyChanged(nameof(RightPlayer));
        OnPropertyChanged(nameof(LeftPlayer));
        OnPropertyChanged(nameof(TopCard));
        OnPropertyChanged(nameof(CartasPendientes));
        OnPropertyChanged(nameof(TienePendientes));
        OnPropertyChanged(nameof(PuedePasar));
        OnPropertyChanged(nameof(ActiveColorHex));
       
        OnPropertyChanged(nameof(HayCartasEnMazo));
        OnPropertyChanged(nameof(BotonPenalizacion));

        // Refrescamos el estado del botón de UNO
        OnPropertyChanged(nameof(CanShoutUno));
    }

    private void ActualizarEstatus()
    {
        _mensajeToken++; 

        if (Game.PilaPendiente > 0)
        {
            EstatusMensaje = $"¡ATENCIÓN {Game.actualJugador.Name}! Hay +{Game.PilaPendiente} acumulados. ¡Responde con +2/+4 o toma el castigo!";
        }
        else
        {
            EstatusMensaje = $"Turno de: {Game.actualJugador.Name} | Mazo: {Game.Pila.Count} cartas";
        }
    }

    public async Task RegistrarJugadaEnServidor(Player jugador, Card carta, string? colorElegido = null)
    {
        ApiService api = new ApiService();
        var datosJugada = new
        {
            jugador_id = jugador.ID,
            partida_id = partidaID,
            color_carta = carta.Color.ToString(),
            valor_carta = carta.Value.ToString(),
            color_elegido = colorElegido 
        };
        string? respuesta = await api.EnviarJugadaLog("/partida/jugar-carta", datosJugada);
        if (respuesta != null) Console.WriteLine("Acción registrada en el servidor correctamente.");
    }
    
    public async Task RegistrarVictoriaEnServidor(Player jugador)
    {
        ApiService api = new ApiService();
        var datosJugada = new { jugador_id = jugador.ID, partida_id = partidaID };
        string? respuesta = await api.EnviarJugadaLog("/partida/gano", datosJugada);
        if (respuesta != null) Console.WriteLine("Acción registrada en el servidor correctamente.");
    }
    
    public async Task RegistrarRoboEnServidor(Player jugador, Card card)
    {
        ApiService api = new ApiService();
        var datosRobo = new
        {
            jugador_id = jugador.ID, 
            partida_id = partidaID, 
            color_carta= card.Color.ToString() , 
            valor_carta = card.Value.ToString()
        };
        string? respuesta = await api.EnviarJugadaLog("/partida/robar-carta", datosRobo);
    }
    
    public async Task RegistrarPenalizacionEnServidor(Player jugador)
    {
        ApiService api = new ApiService();
        await api.EnviarJugadaLog("/partida/tomar-penalizacion", new { jugador_id = jugador.ID, partida_id = partidaID });
    }

    public async Task RegistrarPaseDeTurnoEnServidor(Player jugador)
    {
        ApiService api = new ApiService();
        await api.EnviarJugadaLog("/partida/pasar-turno", new { jugador_id = jugador.ID, partida_id = partidaID });
    }
    
    public async Task RegistrarUno(Player jugador)
    {
        ApiService api = new ApiService();
        await api.EnviarJugadaLog("/partida/uno", new { jugador_id = jugador.ID, partida_id = partidaID, decision=_haDichoUno });
    }
}