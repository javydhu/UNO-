"""
Motor de reglas de UNO que vive en el SERVIDOR.

Es el puerto a Python de lo que antes hacia UnoGame.cs en cada cliente.
Ahora hay UN SOLO mazo, UNA SOLA mano por jugador y UN SOLO turno, y estan aqui.
Los clientes (C#) solo dibujan lo que este motor les devuelve.

Las cartas usan los mismos nombres que los enum de C# (CardColor / CardValue)
para que el cliente pueda hacer Enum.Parse directamente.
"""
import random
import threading

COLORES = ["Red", "Yellow", "Green", "Blue"]
VALORES_NUMERO = ["Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine"]
VALORES_ACCION = ["Skip", "Reverse", "DrawTwo"]
PUNTOS = {valor: i for i, valor in enumerate(VALORES_NUMERO)}  # comodines y especiales valen 0

MIN_JUGADORES = 2
MAX_JUGADORES = 3          # la pantalla tiene 3 asientos: abajo, izquierda y derecha
CARTAS_INICIALES = 7

# Todas las partidas activas viven aqui, en memoria del proceso de la API.
# (Ver nota en la respuesta: no usar --workers > 1 ni --reload.)
PARTIDAS: dict = {}
LOCK = threading.RLock()   # FastAPI atiende peticiones en varios hilos; este candado evita carreras


class ErrorJuego(Exception):
    """Una jugada o accion no valida. main.py la convierte en HTTPException."""

    def __init__(self, mensaje: str, codigo: int = 400):
        super().__init__(mensaje)
        self.mensaje = mensaje
        self.codigo = codigo


def crear_mazo():
    """108 cartas exactas de UNO, ya barajadas."""
    mazo = []
    for color in COLORES:
        mazo.append((color, "Zero"))                      # 1 cero por color (4)
        for _ in range(2):                                # 2 de cada: 1-9, Skip, Reverse, DrawTwo (96)
            for valor in VALORES_NUMERO[1:] + VALORES_ACCION:
                mazo.append((color, valor))
    for _ in range(4):                                    # 4 comodines + 4 comodines +4 (8)
        mazo.append(("Wild", "Wild"))
        mazo.append(("Wild", "WildDrawFour"))
    random.shuffle(mazo)
    return mazo


def _carta_dict(carta):
    return {"color": carta[0], "value": carta[1]}


class EstadoPartida:
    def __init__(self, partida_id: int, host_id: int, host_nombre: str):
        self.partida_id = partida_id
        self.host_id = host_id
        self.jugadores = [host_id]            # el orden de la lista ES el orden de los asientos
        self.nombres = {host_id: host_nombre}
        self.manos = {}                       # jugador_id -> lista de cartas (color, valor)
        self.mazo = []
        self.descarte = []
        self.color_actual = "Red"
        self.turno_idx = 0
        self.direccion = 1
        self.pendientes = 0                   # cartas acumuladas por +2 / +4
        self.pases = 0                        # pases continuos (solo cuando el mazo esta vacio)
        self.estado = "esperando"             # esperando | en_juego | terminada
        self.version = 1                      # sube con CADA cambio; los clientes la usan para hacer polling
        self.ganador_id = None
        self.detalle_final = ""
        self.victoria_registrada = False      # para que main.py guarde la victoria en MySQL una sola vez
        self.dijo_uno = set()
        self.ultimo_evento = ""

    # ------------------------------------------------------------------ utilidades
    def _cerrar(self, logs):
        """Sube la version y guarda el texto del evento. Devuelve los textos para el log de MySQL."""
        self.version += 1
        self.ultimo_evento = " ".join(logs)
        return logs

    def _n(self):
        return len(self.jugadores)

    def _siguiente(self):
        self.turno_idx = (self.turno_idx + self.direccion) % self._n()

    def _validar_en_juego(self, jid):
        if self.estado != "en_juego":
            raise ErrorJuego("La partida no esta en juego.", 409)
        if jid not in self.jugadores:
            raise ErrorJuego("No perteneces a esta partida.", 403)

    def _validar_turno(self, jid):
        self._validar_en_juego(jid)
        if self.jugadores[self.turno_idx] != jid:
            raise ErrorJuego("¡No es tu turno!", 409)

    def _es_jugable(self, carta):
        color, valor = carta
        top = self.descarte[-1]
        return color == "Wild" or color == self.color_actual or valor == top[1]

    def _robar_n(self, jid, n):
        robadas = 0
        while robadas < n and self.mazo:
            self.manos[jid].append(self.mazo.pop(0))
            robadas += 1
        return robadas

    def _puntos(self, jid):
        return sum(PUNTOS.get(valor, 0) for _, valor in self.manos[jid])

    def puntuaciones(self):
        """{jugador_id: puntos} con lo que le quedo en la mano a cada jugador (mismo calculo de _puntos).
        Lo usa la pantalla de estadisticas cuando la partida ya termino."""
        return {j: self._puntos(j) for j in self.jugadores}

    def _terminar(self, ganador_id, detalle):
        self.estado = "terminada"
        self.ganador_id = ganador_id
        self.detalle_final = detalle

    # ------------------------------------------------------------------ sala de espera
    def agregar_jugador(self, jid, nombre):
        """True si se agrego, False si ya estaba (entrar dos veces no duplica el asiento)."""
        if jid in self.jugadores:
            return False
        if self.estado != "esperando":
            raise ErrorJuego("La partida ya comenzo, no puedes unirte.", 409)
        if self._n() >= MAX_JUGADORES:
            raise ErrorJuego(f"La partida esta llena (maximo {MAX_JUGADORES} jugadores).", 409)
        self.jugadores.append(jid)
        self.nombres[jid] = nombre
        self._cerrar([f"{nombre} se unio a la partida."])
        return True

    def iniciar(self, jid):
        """Reparte y empieza. Tambien sirve para REINICIAR una partida (solo el anfitrion)."""
        if jid != self.host_id:
            raise ErrorJuego("Solo el anfitrion puede iniciar la partida.", 403)
        if self._n() < MIN_JUGADORES:
            raise ErrorJuego(f"Se necesitan al menos {MIN_JUGADORES} jugadores para empezar.", 409)

        self.mazo = crear_mazo()
        self.descarte = []
        self.manos = {j: [] for j in self.jugadores}
        for _ in range(CARTAS_INICIALES):
            for j in self.jugadores:
                self.manos[j].append(self.mazo.pop(0))

        # La primera carta de la mesa no puede ser comodin: se regresa al fondo del mazo
        while True:
            primera = self.mazo.pop(0)
            if primera[0] == "Wild":
                self.mazo.append(primera)
                continue
            break
        self.descarte.append(primera)
        self.color_actual = primera[0]

        self.turno_idx = 0
        self.direccion = 1
        self.pendientes = 0
        self.pases = 0
        self.ganador_id = None
        self.detalle_final = ""
        self.victoria_registrada = False
        self.dijo_uno.clear()
        self.estado = "en_juego"
        return self._cerrar([f"¡Comenzo la partida! Empieza {self.nombres[self.jugadores[0]]}."])

    # ------------------------------------------------------------------ jugadas
    def jugar(self, jid, color, valor, color_elegido=None):
        self._validar_turno(jid)
        carta = (color, valor)
        mano = self.manos[jid]
        if carta not in mano:
            raise ErrorJuego("No tienes esa carta en la mano.")

        if self.pendientes > 0:
            if valor not in ("DrawTwo", "WildDrawFour"):
                raise ErrorJuego(f"¡Hay un +{self.pendientes} acumulado! Responde con +2/+4 o toma el castigo.")
        elif not self._es_jugable(carta):
            raise ErrorJuego("Movimiento invalido. Elige una carta del mismo color, numero o comodin.")

        mano.remove(carta)
        self.descarte.append(carta)
        self.pases = 0
        nombre = self.nombres[jid]

        if color == "Wild":
            self.color_actual = color_elegido if color_elegido in COLORES else "Red"
            logs = [f"{nombre} jugo {valor} y cambio el color a {self.color_actual}."]
        else:
            self.color_actual = color
            logs = [f"{nombre} jugo {color} {valor}."]

        if valor == "DrawTwo":
            self.pendientes += 2
        elif valor == "WildDrawFour":
            self.pendientes += 4
        elif valor == "Skip":
            self._siguiente()
        elif valor == "Reverse":
            self.direccion *= -1
            if self._n() == 2:          # con 2 jugadores Reverse funciona como Skip
                self._siguiente()
        self._siguiente()

        if not mano:
            self._terminar(jid, "¡Se ha quedado sin cartas!")
            logs.append(f"¡El jugador {nombre} ha ganado la partida!")
        elif len(mano) == 1 and jid not in self.dijo_uno:
            n = self._robar_n(jid, 2)
            logs.append(f"¡{nombre} olvido decir UNO! Roba {n} cartas de castigo.")
        self.dijo_uno.discard(jid)
        return self._cerrar(logs)

    def robar(self, jid):
        self._validar_turno(jid)
        if self.pendientes > 0:
            raise ErrorJuego(f"Hay un +{self.pendientes} acumulado: juega +2/+4 o toma el castigo.")
        if not self.mazo:
            raise ErrorJuego("El mazo esta vacio.")
        self._robar_n(jid, 1)
        self.dijo_uno.discard(jid)
        nombre = self.nombres[jid]
        logs = [f"{nombre} robo una carta."]
        if not self.mazo:
            logs.append("Se acabo el mazo.")
        return self._cerrar(logs)

    def tomar_penalizacion(self, jid):
        self._validar_turno(jid)
        if self.pendientes == 0:
            raise ErrorJuego("No tienes ninguna penalizacion pendiente.")
        total = self.pendientes
        n = self._robar_n(jid, total)
        self.pendientes = 0
        self.dijo_uno.discard(jid)
        self._siguiente()
        nombre = self.nombres[jid]
        return self._cerrar([f"{nombre} no pudo responder y robo {n} cartas (castigo de +{total})."])

    def pasar(self, jid):
        self._validar_turno(jid)
        if self.mazo or self.pendientes > 0:
            raise ErrorJuego("Solo puedes pasar cuando el mazo esta vacio.")
        self.pases += 1
        self.dijo_uno.discard(jid)
        nombre = self.nombres[jid]
        logs = [f"{nombre} paso su turno."]

        if self.pases >= self._n():
            orden = sorted(self.jugadores, key=self._puntos)   # sorted es estable: empate -> primer asiento
            detalle = "PUNTUACION FINAL:\n" + "\n".join(
                f"{self.nombres[j]}: {self._puntos(j)} pts" for j in orden
            )
            self._terminar(orden[0], detalle)
            logs.append(f"¡El jugador {self.nombres[orden[0]]} ha ganado la partida por puntos!")
        else:
            self._siguiente()
        return self._cerrar(logs)

    def decir_uno(self, jid, decision=True):
        self._validar_en_juego(jid)
        nombre = self.nombres[jid]
        if decision:
            self.dijo_uno.add(jid)
            return self._cerrar([f"¡{nombre} dijo UNO!"])
        self.dijo_uno.discard(jid)
        return self._cerrar([f"¡{nombre} no dijo UNO!"])

    # ------------------------------------------------------------------ lo que ve cada cliente
    def a_dict(self, jugador_id=None):
        """Estado completo de la partida. Solo incluye las cartas del jugador que pregunta;
        de los demas solo se manda cuantas cartas tienen."""
        en_juego = self.estado != "esperando"
        top = self.descarte[-1] if self.descarte else None
        return {
            "sin_cambios": False,
            "partida_id": self.partida_id,
            "version": self.version,
            "estado": self.estado,
            "host_id": self.host_id,
            "jugadores": [
                {
                    "id": j,
                    "nombre": self.nombres[j],
                    "posicion": pos,
                    "cartas": len(self.manos.get(j, [])),
                    "es_turno": self.estado == "en_juego" and pos == self.turno_idx,
                }
                for pos, j in enumerate(self.jugadores)
            ],
            "mi_mano": [_carta_dict(c) for c in self.manos.get(jugador_id, [])],
            "top_card": _carta_dict(top) if top else None,
            "color_actual": self.color_actual,
            "turno_actual_jugador_id": self.jugadores[self.turno_idx] if en_juego else None,
            "direccion": self.direccion,
            "pendientes": self.pendientes,
            "cartas_en_mazo": len(self.mazo),
            "dijo_uno": jugador_id in self.dijo_uno,
            "ganador_id": self.ganador_id,
            "ganador_nombre": self.nombres.get(self.ganador_id, "") if self.ganador_id else "",
            "detalle_final": self.detalle_final,
            "ultimo_evento": self.ultimo_evento,
        }
