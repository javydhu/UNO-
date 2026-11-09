using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Threading;
using Uno.Models;
using Uno.Services;

namespace Uno.ViewModels;

public class MainViewModel : ViewModelBase
{
    private const int MaxJugadores = 3;   // debe coincidir con MAX_JUGADORES de game_engine.py
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private readonly ApiService _api = new();

    // Foto del estado del servidor
    private Player? _bottomPlayer;
    private Player? _leftPlayer;
    private Player? _rightPlayer;
    private Card? _topCard;
    private CardColor _colorActual = CardColor.Red;
    private int _turnoId;
    private int _pendientes;
    private int _cartasEnMazo;
    private int _ultimaVersion;
    private bool _haDichoUno;
    private string _ultimoEvento = string.Empty;
    private Dictionary<int, string> _nombres = new();

    // Identidad local
    private int _miId;
    private int partidaID;
    private bool _esHost;

    // Estado de pantallas
    private string _estatusMensaje = string.Empty;
    private string _mensajeMenu = string.Empty;
    private string _salaTexto = string.Empty;
    private int _jugadoresEnSala;
    private bool _JuegoIniciado = false;
    private bool _enSala = false;
    private bool _ColorElegidoVisible = false;
    private bool _JuegoTerminado = false;
    private string _ganadorNombre = string.Empty;
    private string _gameOverDetails = string.Empty;
    private Card? _CartaWildPendiente = null;
    private bool _menuPausaVisible = false;
    private bool _isProcessingPlay = false;
    private bool _isStartingGame = false;
    private string _nombreUsuario = "Jugador_" + Random.Shared.Next(100, 1000); // distinto en cada PC
    private string _ipServidor = "127.0.0.1";
    private string _idPartidaInput = string.Empty;
    private int _mensajeToken = 0; // Ayuda a cancelar mensajes viejos si suceden rapido

    // Solo presentacion (no cambian las reglas)
    private int _direccion = 1;
    private bool _ganoYo;
    private string _partidaIdTexto = string.Empty;
    private string _contadorSala = string.Empty;
    private string _colorJugadorHex = PaletaJugador[PaletaJugador.Length - 1].Hex;
    private Dictionary<int, string> _coloresPorId = new();
    private EstadoPartidaDto? _ultimoEstado;   // ultima foto aplicada, para repintar si el usuario cambia su color

    // Estadisticas de la pantalla de victoria (solo presentacion; los datos los entrega GET /partida/{id}/estadisticas)
    private const double MaxAlturaBarra = 120;   // alto en pixeles de la barra con mas victorias
    private bool _estadisticasVisibles = false;
    private bool _cargandoEstadisticas = false;
    private string _errorEstadisticas = string.Empty;

    // Polling 
    private DispatcherTimer? _timer;
    private bool _sincronizando = false;
    private int _fallosConexion = 0;

    public class JugadorRespuesta
    {
        public string Nombre { get; set; } = string.Empty;
        public int Id { get; set; }
        [JsonPropertyName("partidas_ganadas")]
        public int PartidasGanadas { get; set; }
    }

    // propiedades de entrada
    public string IDPartidaInput
    {
        get => _idPartidaInput;
        set { if (SetProperty(ref _idPartidaInput, value)) OnPropertyChanged(nameof(TextoBotonEntrar)); }
    }

    public string NombreUsuario
    {
        get => _nombreUsuario;
        set { if (SetProperty(ref _nombreUsuario, value)) OnPropertyChanged(nameof(InicialUsuario)); }
    }

    public string IPServidor
    {
        get => _ipServidor;
        set => SetProperty(ref _ipServidor, value);
    }

    // propiedades de pantalla
    public string EstatusMensaje
    {
        get => _estatusMensaje;
        set => SetProperty(ref _estatusMensaje, value);
    }

    // Errores y avisos para el menu principal y la sala de espera (el banner del tablero no se ve ahi)
    public string MensajeMenu
    {
        get => _mensajeMenu;
        set => SetProperty(ref _mensajeMenu, value);
    }

    public string SalaTexto
    {
        get => _salaTexto;
        set => SetProperty(ref _salaTexto, value);
    }

    // true cuando la partida ya fue repartida y se muestra la mesa
    public bool JuegoIniciado
    {
        get => _JuegoIniciado;
        set { if (SetProperty(ref _JuegoIniciado, value)) OnPropertyChanged(nameof(MenuVisible)); }
    }

    // true cuando ya entraste a una partida pero el anfitrion todavia no la inicia
    public bool EnSala
    {
        get => _enSala;
        set { if (SetProperty(ref _enSala, value)) OnPropertyChanged(nameof(MenuVisible)); }
    }

    public bool MenuVisible => !JuegoIniciado && !EnSala;

    public bool EsHost
    {
        get => _esHost;
        set { if (SetProperty(ref _esHost, value)) { OnPropertyChanged(nameof(PuedeIniciar)); OnPropertyChanged(nameof(HostEsperaJugadores)); } }
    }

    public bool PuedeIniciar => EsHost && _jugadoresEnSala >= 2;

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

    public bool MenuPausaVisible
    {
        get => _menuPausaVisible;
        set => SetProperty(ref _menuPausaVisible, value);
    }

    // ------------------------------------------------------------------
    // ESTADISTICAS (se muestran en el mismo cuadro de la pantalla de victoria)
    // ------------------------------------------------------------------
    public bool EstadisticasVisibles
    {
        get => _estadisticasVisibles;
        set { if (SetProperty(ref _estadisticasVisibles, value)) OnPropertyChanged(nameof(TextoBotonEstadisticas)); }
    }

    public string TextoBotonEstadisticas => EstadisticasVisibles ? "Ocultar estadísticas" : "Mostrar estadísticas";

    public bool CargandoEstadisticas
    {
        get => _cargandoEstadisticas;
        private set => SetProperty(ref _cargandoEstadisticas, value);
    }

    public string ErrorEstadisticas
    {
        get => _errorEstadisticas;
        private set => SetProperty(ref _errorEstadisticas, value);
    }

    // Grafica de barras: un jugador por barra, de mas a menos partidas ganadas
    public ObservableCollection<EstadisticaJugador> EstadisticasBarras { get; } = new();
    // Tabla: un jugador por fila con sus puntos, de menos a mas puntos (igual que decide el motor)
    public ObservableCollection<EstadisticaJugador> EstadisticasTabla { get; } = new();
    public bool HayEstadisticas => EstadisticasTabla.Count > 0;

    // propiedades calculadas
    public Card? TopCard => _topCard;
    public bool EsMiTurno => JuegoIniciado && !JuegoTerminado && _turnoId == _miId;
    public int CartasPendientes => _pendientes;
    // Los botones de castigo / pasar solo aparecen cuando ES TU TURNO
    public bool TienePendientes => EsMiTurno && _pendientes > 0;
    public bool PuedePasar => EsMiTurno && _cartasEnMazo == 0 && _pendientes == 0;
    public bool HayCartasEnMazo => _cartasEnMazo > 0;
    public string BotonPenalizacion => _cartasEnMazo == 0
        ? "Pasar Turno"
        : $"Tomar +{_pendientes} y Perder Turno";

    // El boton de UNO aparece solo en tu turno, con 2 cartas, si alguna es jugable y aun no lo has presionado
    public bool CanShoutUno => EsMiTurno
                            && _bottomPlayer != null
                            && _bottomPlayer.Mano.Count == 2
                            && _bottomPlayer.Mano.Any(c => c.esJugable)
                            && !_haDichoUno;

    public string ActiveColorHex => _colorActual switch
    {
        CardColor.Red => "#ED2939",
        CardColor.Yellow => "#FFD60A",
        CardColor.Blue => "#2D7FF9",
        CardColor.Green => "#1FAA59",
        _ => "#888888"
    };

    // ------------------------------------------------------------------
    // PRESENTACION: textos y banderas que solo usa la vista
    // ------------------------------------------------------------------
    public string ColorActualNombre => _colorActual switch
    {
        CardColor.Red => "ROJO",
        CardColor.Yellow => "AMARILLO",
        CardColor.Blue => "AZUL",
        CardColor.Green => "VERDE",
        _ => "-"
    };

    public bool HayPendientes => _pendientes > 0;
    public string PendientesTexto => $"+{_pendientes} ACUMULADO";
    public int CartasEnMazo => _cartasEnMazo;
    // Direccion 1 = el turno avanza al asiento de la derecha (abajo -> derecha -> izquierda): antihorario en pantalla
    public string SentidoTexto => _direccion >= 0 ? "Sentido antihorario" : "Sentido horario";

    public bool GanoYo
    {
        get => _ganoYo;
        set => SetProperty(ref _ganoYo, value);
    }

    public bool EstaConectando => _isStartingGame;
    public string TextoBotonEntrar => _isStartingGame
        ? "CONECTANDO..."
        : (string.IsNullOrWhiteSpace(IDPartidaInput) ? "CREAR PARTIDA" : "UNIRME A LA PARTIDA");

    // Sala de espera
    public ObservableCollection<LobbyJugador> JugadoresSala { get; } = new();
    public string PartidaIdTexto
    {
        get => _partidaIdTexto;
        set => SetProperty(ref _partidaIdTexto, value);
    }
    public string ContadorSala
    {
        get => _contadorSala;
        set => SetProperty(ref _contadorSala, value);
    }
    public bool HostEsperaJugadores => EsHost && !PuedeIniciar;

    // ------------------------------------------------------------------
    // COLOR DEL JUGADOR (el usuario elige con que color se ve su nombre)
    // ------------------------------------------------------------------
    private static readonly (string Nombre, string Hex)[] PaletaJugador =
    {
        ("Dorado",   "#FFD60A"),
        ("Cian",     "#22D3EE"),
        ("Rosa",     "#FF6FB5"),
        ("Lima",     "#A3E635"),
        ("Naranja",  "#FF9F43"),
        ("Violeta",  "#A78BFA"),
        ("Coral",    "#FF6B6B"),
        ("Azul",     "#60A5FA"),
    };

    public ObservableCollection<PlayerColorOption> ColoresDisponibles { get; } = new();

    public string ColorJugadorHex
    {
        get => _colorJugadorHex;
        private set => SetProperty(ref _colorJugadorHex, value);
    }

    public string ColorJugadorNombre
    {
        get
        {
            foreach (var c in PaletaJugador)
                if (c.Hex == _colorJugadorHex) return c.Nombre;
            return string.Empty;
        }
    }

    public string InicialUsuario => Player.InicialDe(NombreUsuario);

    private void ElegirColor(PlayerColorOption opcion)
    {
        ColorJugadorHex = opcion.Hex;
        foreach (var c in ColoresDisponibles) c.IsSelected = ReferenceEquals(c, opcion);
        OnPropertyChanged(nameof(ColorJugadorNombre));

        // Si ya hay sala o partida en pantalla se repinta con el color nuevo (no se manda nada al servidor)
        if (_ultimoEstado != null && (EnSala || JuegoIniciado))
            AplicarEstado(_ultimoEstado, forzar: true);
    }

    // Yo uso el color que elegi; los demas reciben uno de la paleta (siempre distinto al mio y entre si)
    private void AsignarColores(List<JugadorEstadoDto> jugadores)
    {
        var usados = new HashSet<string> { ColorJugadorHex };
        var dic = new Dictionary<int, string>();
        foreach (var j in jugadores.OrderBy(x => x.Posicion))
        {
            if (j.Id == _miId) { dic[j.Id] = ColorJugadorHex; continue; }
            int inicio = Math.Abs(j.Id) % PaletaJugador.Length;
            for (int k = 0; k < PaletaJugador.Length; k++)
            {
                string hex = PaletaJugador[(inicio + k) % PaletaJugador.Length].Hex;
                if (usados.Add(hex)) { dic[j.Id] = hex; break; }
            }
        }
        _coloresPorId = dic;
    }

    private string ColorDe(int id) => _coloresPorId.TryGetValue(id, out var hex) ? hex : "#FFD60A";

    private void ReconstruirSala(EstadoPartidaDto e)
    {
        JugadoresSala.Clear();
        foreach (var jug in e.Jugadores.OrderBy(x => x.Posicion))
        {
            JugadoresSala.Add(new LobbyJugador
            {
                Nombre = jug.Nombre,
                Inicial = Player.InicialDe(jug.Nombre),
                ColorHex = ColorDe(jug.Id),
                EsAnfitrion = jug.Id == e.HostId,
                EsYo = jug.Id == _miId
            });
        }
        for (int i = e.Jugadores.Count; i < MaxJugadores; i++)
            JugadoresSala.Add(new LobbyJugador { EsVacio = true });

        PartidaIdTexto = e.PartidaId.ToString();
        ContadorSala = $"{e.Jugadores.Count} de {MaxJugadores} jugadores";
        OnPropertyChanged(nameof(HostEsperaJugadores));
    }

    // PERSPECTIVA: ahora son fijas por asiento. Abajo SIEMPRE eres tu; a la derecha va el siguiente
    // asiento y a la izquierda el anterior. Se calculan en AplicarEstado().
    public Player? BottomPlayer => _bottomPlayer;
    public Player? RightPlayer => _rightPlayer;
    public Player? LeftPlayer => _leftPlayer;

    public MainViewModel()
    {
        foreach (var c in PaletaJugador)
        {
            ColoresDisponibles.Add(new PlayerColorOption(c.Nombre, c.Hex)
            {
                SelectAction = ElegirColor,
                IsSelected = c.Hex == _colorJugadorHex
            });
        }
    }

    private string NombreDe(int id) => _nombres.TryGetValue(id, out var n) ? n : "?";

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

    // ENTRAR A UNA PARTIDA
    // Se usa para CREAR (ID vacio) o UNIRSE (con ID). Ya NO crea ningun mazo local.
    public async Task StartGame()
    {
        if (_isStartingGame) return;
        _isStartingGame = true;
        OnPropertyChanged(nameof(EstaConectando));
        OnPropertyChanged(nameof(TextoBotonEntrar));

        try
        {
            MensajeMenu = string.Empty;
            ApiService.ServerIP = string.IsNullOrWhiteSpace(IPServidor) ? "127.0.0.1" : IPServidor.Trim();
            DetenerPolling();

            // 1. Registro del jugador en el servidor
            string nombreFinal = string.IsNullOrWhiteSpace(NombreUsuario) ? "Jugador" : NombreUsuario.Trim();
            ApiResult login = await _api.PostAsync("/jugadores/login", new { nombre = nombreFinal });
            if (!login.Ok)
            {
                MensajeMenu = ApiService.ExtraerDetalle(login);
                return;
            }

            try
            {
                var jugadorServidor = JsonSerializer.Deserialize<JugadorRespuesta>(login.Body, _json);
                if (jugadorServidor == null || jugadorServidor.Id <= 0)
                {
                    MensajeMenu = "Respuesta invalida del servidor al iniciar sesion.";
                    return;
                }
                _miId = jugadorServidor.Id;
            }
            catch (JsonException e)
            {
                Console.WriteLine(e.Message);
                MensajeMenu = "Respuesta invalida del servidor al iniciar sesion.";
                return;
            }

            // 2. CREA una partida nueva (host) o se UNE a una existente
            ApiResult resp;
            if (int.TryParse(IDPartidaInput?.Trim(), out int idExistente) && idExistente > 0)
            {
                resp = await _api.PostAsync("/partida/unirse", new { partida_id = idExistente, jugador_id = _miId });
            }
            else
            {
                resp = await _api.PostAsync("/partida/nueva", new { jugador_id = _miId });
            }

            if (!resp.Ok)
            {
                MensajeMenu = ApiService.ExtraerDetalle(resp);
                return;
            }

            EstadoPartidaDto? estado;
            try { estado = JsonSerializer.Deserialize<EstadoPartidaDto>(resp.Body, _json); }
            catch (JsonException) { estado = null; }
            if (estado == null)
            {
                MensajeMenu = "Respuesta invalida del servidor.";
                return;
            }

            // 3. Se limpia el estado que pudo quedar de la partida anterior y se carga el del servidor
            partidaID = estado.PartidaId;
            _ultimaVersion = 0;
            _haDichoUno = false;
            _ultimoEvento = string.Empty;
            _CartaWildPendiente = null;
            ColorElegidoVisible = false;
            MenuPausaVisible = false;
            JuegoTerminado = false;
            ReiniciarEstadisticas();
            JuegoIniciado = false;
            EnSala = false;

            AplicarEstado(estado, forzar: true);
            IniciarPolling();
        }
        finally
        {
            _isStartingGame = false;
            OnPropertyChanged(nameof(EstaConectando));
            OnPropertyChanged(nameof(TextoBotonEntrar));
        }
    }

    // POLLING
    private void IniciarPolling()
    {
        _fallosConexion = 0;
        if (_timer == null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _timer.Tick += OnPollTick;
        }
        _timer.Start();
    }

    private void DetenerPolling()
    {
        _timer?.Stop();
    }

    private async void OnPollTick(object? sender, EventArgs e)
    {
        await SincronizarAsync();
    }

    private async Task SincronizarAsync()
    {
        if (_sincronizando || (!JuegoIniciado && !EnSala)) return; // sin partida activa no hay nada que consultar
        _sincronizando = true;

        try
        {
            ApiResult r = await _api.GetAsync($"/partida/{partidaID}/estado?jugador_id={_miId}&desde_version={_ultimaVersion}");

            if (r.Status == 404)
            {
                QuitToMainMenu();
                MensajeMenu = "La partida ya no existe en el servidor (se reinicio la API).";
                return;
            }

            if (!r.Ok)
            {
                _fallosConexion++;
                if (_fallosConexion >= 3)
                {
                    EstatusMensaje = "Conexion perdida con el servidor. Reintentando...";
                    MensajeMenu = "Conexion perdida con el servidor. Reintentando...";
                }
                return;
            }

            if (_fallosConexion >= 3)
            {
                MensajeMenu = string.Empty;
                ActualizarEstatus();
            }
            _fallosConexion = 0;

            var estado = JsonSerializer.Deserialize<EstadoPartidaDto>(r.Body, _json);
            if (estado == null || estado.SinCambios) return;
            AplicarEstado(estado);
        }
        catch (JsonException e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            _sincronizando = false;
        }
    }

    // APLICAR EL ESTADO DEL SERVIDOR
    private void AplicarEstado(EstadoPartidaDto e, bool forzar = false)
    {
        // Una respuesta vieja (por ejemplo un polling que tardo) nunca debe pisar una mas nueva
        if (!forzar && e.Version < _ultimaVersion) return;
        bool hayCambio = forzar || e.Version != _ultimaVersion;
        _ultimaVersion = e.Version;
        _ultimoEstado = e;

        _nombres = e.Jugadores.ToDictionary(j => j.Id, j => j.Nombre);
        AsignarColores(e.Jugadores);
        EsHost = e.HostId == _miId;

        // Sala de espera: todavia no se reparte
        if (e.Estado == "esperando")
        {
            _jugadoresEnSala = e.Jugadores.Count;
            var sb = new StringBuilder();
            sb.AppendLine($"ID de partida: {e.PartidaId}");
            sb.AppendLine("Compartelo con los demas jugadores para que se unan.");
            sb.AppendLine();
            sb.AppendLine($"Jugadores ({e.Jugadores.Count}/{MaxJugadores}):");
            foreach (var jug in e.Jugadores.OrderBy(x => x.Posicion))
            {
                string etiqueta = jug.Id == e.HostId ? " (anfitrion)" : string.Empty;
                string yo = jug.Id == _miId ? " <- tu" : string.Empty;
                sb.AppendLine($"  • {jug.Nombre}{etiqueta}{yo}");
            }
            SalaTexto = sb.ToString();
            ReconstruirSala(e);
            JuegoIniciado = false;
            JuegoTerminado = false;
            ReiniciarEstadisticas();
            EnSala = true;
            OnPropertyChanged(nameof(PuedeIniciar));
            return;
        }

        // Partida en juego o terminada 
        EnSala = false;
        JuegoIniciado = true;

        // PERSPECTIVA: se ordena por asiento real y se rota para que YO quede abajo
        var orden = e.Jugadores.OrderBy(j => j.Posicion).ToList();
        int m = orden.FindIndex(j => j.Id == _miId);
        if (m < 0)
        {
            QuitToMainMenu();
            MensajeMenu = "No perteneces a esta partida.";
            return;
        }
        int n = orden.Count;

        _bottomPlayer = CrearJugadorLocal(orden[m], e.MiMano);
        _rightPlayer = n >= 2 ? CrearOponente(orden[(m + 1) % n]) : null;   // siguiente asiento
        _leftPlayer = n >= 3 ? CrearOponente(orden[(m + n - 1) % n]) : null; // asiento anterior

        _topCard = e.TopCard != null ? CrearCarta(e.TopCard) : null;
        _colorActual = Enum.TryParse<CardColor>(e.ColorActual, out var color) ? color : CardColor.Red;
        _turnoId = e.TurnoActualJugadorId ?? 0;
        _pendientes = e.Pendientes;
        _cartasEnMazo = e.CartasEnMazo;
        _haDichoUno = e.DijoUno;
        _ultimoEvento = e.UltimoEvento;
        _direccion = e.Direccion;

        if (e.Estado == "terminada")
        {
            GanoYo = e.GanadorId == _miId;
            GanadorNombre = e.GanadorNombre;
            GameOverDetails = e.DetalleFinal;
            ColorElegidoVisible = false;
            _CartaWildPendiente = null;
            JuegoTerminado = true;
        }
        else
        {
            JuegoTerminado = false;
            ReiniciarEstadisticas();
            ColorElegidoVisible = _CartaWildPendiente != null && EsMiTurno && ColorElegidoVisible;
            if (!ColorElegidoVisible) _CartaWildPendiente = null;
        }

        if (hayCambio)
        {
            NotifyAllPositions();
            ActualizarEstatus();
        }
    }

    private Card? CrearCarta(CartaDto dto)
    {
        if (Enum.TryParse<CardColor>(dto.Color, out var c) && Enum.TryParse<CardValue>(dto.Value, out var v))
            return new Card(c, v);
        return null;
    }

    // Mi jugador: con todas mis cartas reales y clicables
    private Player CrearJugadorLocal(JugadorEstadoDto info, List<CartaDto> mano)
    {
        var p = new Player(info.Id, info.Nombre) { ColorHex = ColorDe(info.Id), EsTurno = info.EsTurno };
        foreach (var dto in mano)
        {
            var carta = CrearCarta(dto);
            if (carta == null) continue;
            carta.PlayAction = JugarCarta;
            p.Mano.Add(carta);
        }
        return p;
    }

    // Oponente: solo se conoce CUANTAS cartas tiene; se llenan con cartas de relleno (la vista solo muestra el reverso)
    private Player CrearOponente(JugadorEstadoDto info)
    {
        var p = new Player(info.Id, info.Nombre)
        {
            ColorHex = ColorDe(info.Id),
            EsTurno = info.EsTurno,
            TieneUnaCarta = info.Cartas == 1
        };
        for (int i = 0; i < info.Cartas; i++)
            p.Mano.Add(new Card(CardColor.Wild, CardValue.Wild));
        return p;
    }

    // ACCIONES DEL JUGADOR
    // Todas mandan la accion al servidor; el servidor valida (turno, reglas) y regresa el estado nuevo.
    private async Task EnviarAccionAsync(string endpoint, object? datos = null)
    {
        if (_isProcessingPlay) return;
        _isProcessingPlay = true;

        try
        {
            datos ??= new { jugador_id = _miId, partida_id = partidaID };
            ApiResult r = await _api.PostAsync(endpoint, datos);

            if (r.Ok)
            {
                var estado = JsonSerializer.Deserialize<EstadoPartidaDto>(r.Body, _json);
                if (estado != null) AplicarEstado(estado);
                if (EnSala) MensajeMenu = string.Empty;
            }
            else
            {
                string detalle = ApiService.ExtraerDetalle(r);
                if (EnSala) MensajeMenu = detalle;
                else MensajeTemporal(detalle, 4000);
            }
        }
        catch (JsonException e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            _isProcessingPlay = false;
        }
    }

    public void JugarCarta(Card card)
    {
        if (JuegoTerminado || _isProcessingPlay) return;
        if (!EsMiTurno)
        {
            MensajeTemporal("¡No es tu turno!", 3000);
            return;
        }

        if (card.Color == CardColor.Wild)
        {
            _CartaWildPendiente = card;
            ColorElegidoVisible = true;
            return;
        }
        EjecutarJugada(card, null);
    }

    public void SeleccionarColor(object? parameter)
    {
        if (parameter is string colorName && _CartaWildPendiente != null)
        {
            if (Enum.TryParse<CardColor>(colorName, out var chosenColor))
            {
                ColorElegidoVisible = false;
                var carta = _CartaWildPendiente;
                _CartaWildPendiente = null;
                EjecutarJugada(carta, chosenColor);
            }
        }
    }

    private async void EjecutarJugada(Card card, CardColor? chosenColor)
    {
        await EnviarAccionAsync("/partida/jugar-carta", new
        {
            jugador_id = _miId,
            partida_id = partidaID,
            color_carta = card.Color.ToString(),
            valor_carta = card.Value.ToString(),
            color_elegido = chosenColor?.ToString()
        });
    }

    // BOTON ¡UNO!
    public async void DecirUno()
    {
        await EnviarAccionAsync("/partida/uno", new { jugador_id = _miId, partida_id = partidaID, decision = true });
    }

    public async void DrawCard()
    {
        if (JuegoTerminado) return;
        if (!EsMiTurno)
        {
            MensajeTemporal("¡No es tu turno!", 3000);
            return;
        }
        await EnviarAccionAsync("/partida/robar-carta");
    }

    public async void TomarPenalizacion()
    {
        if (JuegoTerminado) return;
        await EnviarAccionAsync("/partida/tomar-penalizacion");
    }

    public async void PasarTurno()
    {
        if (JuegoTerminado || !PuedePasar) return;
        await EnviarAccionAsync("/partida/pasar-turno");
    }

    // Boton de la sala de espera (solo lo ve el anfitrion)
    public async Task IniciarPartida()
    {
        await EnviarAccionAsync("/partida/iniciar");
    }

    // Reinicia la partida con los mismos jugadores (solo el anfitrion). Los demas la ven reiniciar por polling.
    public async Task RestartGame()
    {
        MenuPausaVisible = false;
        if (!EsHost)
        {
            MensajeTemporal("Solo el anfitrion puede reiniciar la partida.", 3000);
            return;
        }
        await EnviarAccionAsync("/partida/iniciar");
    }

    public void TogglePauseMenu()
    {
        MenuPausaVisible = !MenuPausaVisible;
    }

    // Boton "Mostrar estadisticas" / "Ocultar estadisticas" de la pantalla de victoria.
    // Los datos se piden al servidor cada vez que se muestran, asi las partidas ganadas siempre estan al dia.
    public async Task AlternarEstadisticas()
    {
        if (EstadisticasVisibles)
        {
            EstadisticasVisibles = false;
            return;
        }
        EstadisticasVisibles = true;
        await CargarEstadisticas();
    }

    public async Task CargarEstadisticas()
    {
        if (CargandoEstadisticas) return;
        CargandoEstadisticas = true;
        ErrorEstadisticas = string.Empty;
        EstadisticasBarras.Clear();
        EstadisticasTabla.Clear();
        OnPropertyChanged(nameof(HayEstadisticas));

        try
        {
            ApiResult r = await _api.GetAsync($"/partida/{partidaID}/estadisticas");
            if (!JuegoTerminado) return;   // mientras llegaba la respuesta se reinicio la partida o se salio al menu
            if (!r.Ok)
            {
                ErrorEstadisticas = ApiService.ExtraerDetalle(r);
                return;
            }

            var dto = JsonSerializer.Deserialize<EstadisticasPartidaDto>(r.Body, _json);
            if (dto == null)
            {
                ErrorEstadisticas = "Respuesta invalida del servidor.";
                return;
            }
            ConstruirEstadisticas(dto);
        }
        catch (JsonException e)
        {
            Console.WriteLine(e.Message);
            ErrorEstadisticas = "Respuesta invalida del servidor.";
        }
        finally
        {
            CargandoEstadisticas = false;
        }
    }

    // Convierte la respuesta del servidor en las filas que dibujan la grafica y la tabla
    private void ConstruirEstadisticas(EstadisticasPartidaDto dto)
    {
        int maxVictorias = dto.Jugadores.Count == 0 ? 0 : dto.Jugadores.Max(j => j.PartidasGanadas);

        var filas = dto.Jugadores.OrderBy(j => j.Posicion).Select(j => new EstadisticaJugador
        {
            Id = j.Id,
            Nombre = j.Nombre,
            ColorHex = ColorDe(j.Id),
            Puntos = j.Puntos,
            PartidasGanadas = j.PartidasGanadas,
            AlturaBarra = (maxVictorias > 0 && j.PartidasGanadas > 0)
                ? Math.Max(4, MaxAlturaBarra * j.PartidasGanadas / maxVictorias)
                : 0,
            EsGanador = j.Id == dto.GanadorId,
            EsYo = j.Id == _miId
        }).ToList();

        // OrderBy es estable: en un empate se respeta el orden de asiento
        foreach (var f in filas.OrderByDescending(x => x.PartidasGanadas)) EstadisticasBarras.Add(f);
        foreach (var f in filas.OrderBy(x => x.Puntos)) EstadisticasTabla.Add(f);
        OnPropertyChanged(nameof(HayEstadisticas));
    }

    // Oculta y vacia las estadisticas (nueva partida, salir al menu...)
    private void ReiniciarEstadisticas()
    {
        EstadisticasVisibles = false;
        ErrorEstadisticas = string.Empty;
        if (EstadisticasTabla.Count > 0 || EstadisticasBarras.Count > 0)
        {
            EstadisticasBarras.Clear();
            EstadisticasTabla.Clear();
            OnPropertyChanged(nameof(HayEstadisticas));
        }
    }

    // Abandona la partida y vuelve a la pantalla inicial
    public void QuitToMainMenu()
    {
        DetenerPolling();
        MenuPausaVisible = false;
        ColorElegidoVisible = false;
        JuegoIniciado = false;
        EnSala = false;
        JuegoTerminado = false;
        ReiniciarEstadisticas();
        _ultimaVersion = 0;
        _ultimoEstado = null;
        MensajeMenu = string.Empty;
    }

    // REFRESCO DE PANTALLA
    private void ActualizarCartasJugables()
    {
        if (_bottomPlayer == null) return;

        foreach (var card in _bottomPlayer.Mano)
        {
            if (!EsMiTurno || _topCard == null)
            {
                card.esJugable = false;
            }
            else if (_pendientes > 0)
            {
                card.esJugable = (card.Value == CardValue.DrawTwo || card.Value == CardValue.WildDrawFour);
            }
            else
            {
                card.esJugable = card.Jugable(_topCard, _colorActual);
            }
        }
    }

    private void NotifyAllPositions()
    {
        ActualizarCartasJugables();
        OnPropertyChanged(nameof(BottomPlayer));
        OnPropertyChanged(nameof(RightPlayer));
        OnPropertyChanged(nameof(LeftPlayer));
        OnPropertyChanged(nameof(TopCard));
        OnPropertyChanged(nameof(EsMiTurno));
        OnPropertyChanged(nameof(CartasPendientes));
        OnPropertyChanged(nameof(TienePendientes));
        OnPropertyChanged(nameof(PuedePasar));
        OnPropertyChanged(nameof(ActiveColorHex));
        OnPropertyChanged(nameof(HayCartasEnMazo));
        OnPropertyChanged(nameof(BotonPenalizacion));
        OnPropertyChanged(nameof(CanShoutUno));
        OnPropertyChanged(nameof(HayPendientes));
        OnPropertyChanged(nameof(PendientesTexto));
        OnPropertyChanged(nameof(CartasEnMazo));
        OnPropertyChanged(nameof(ColorActualNombre));
        OnPropertyChanged(nameof(SentidoTexto));
    }

    private void ActualizarEstatus()
    {
        _mensajeToken++;

        if (JuegoTerminado)
        {
            EstatusMensaje = $"¡JUEGO FINALIZADO! Ganador: {GanadorNombre}";
            return;
        }
        if (!JuegoIniciado) return;

        string nombreTurno = NombreDe(_turnoId);
        string turno;
        if (_pendientes > 0)
        {
            turno = EsMiTurno
                ? $"¡ATENCIÓN {nombreTurno}! Hay +{_pendientes} acumulados. ¡Responde con +2/+4 o toma el castigo!"
                : $"Turno de {nombreTurno} - hay +{_pendientes} acumulados";
        }
        else
        {
            turno = EsMiTurno
                ? $"¡ES TU TURNO! | Mazo: {_cartasEnMazo} cartas"
                : $"Turno de: {nombreTurno} | Mazo: {_cartasEnMazo} cartas";
        }

        EstatusMensaje = string.IsNullOrEmpty(_ultimoEvento) ? turno : $"{_ultimoEvento}  ->  {turno}";
    }
}