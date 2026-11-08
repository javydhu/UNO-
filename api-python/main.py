#Es la que se encarga de la conexion principal que es engine
#y la que nos deja hacer las consultas temporales con get_db
from database import engine, get_db

#depends nos ayuda a hacer inyeccion de dependencias,
#es para que fastapi gestione la apertura y el cierre de
#la conexion que tenemos a la base de datos que esta en
#get_db
from fastapi import Depends, FastAPI, HTTPException

#Son las tablas que hicimos en el archivo models
import models

#NUEVO: el motor de reglas. Aqui viven el mazo, las manos y el turno de cada partida
from game_engine import PARTIDAS, LOCK, ErrorJuego, EstadoPartida

#Se utliza para crear los moldes de los datos, es el que ayuda
#a revisar que los datos que mando C# como peticion en JSON
#son los correctos para poder hacer la query
from pydantic import BaseModel

#Es la libreria que tiene los tipos de datos que vamos a
#ocupar durante la peticion
from sqlalchemy.orm import Session

#Que se creen en la base de datos en caso de que no esten
models.Base.metadata.create_all(bind=engine)

app = FastAPI(title="UNO Game API")

# Modelos de las peticiones
class JugadorCreate(BaseModel):
    nombre: str

class MovimientoCreate(BaseModel):
    partida_id: int
    jugador_id: int
    accion: str

class NuevaPartidaRequest(BaseModel):
    jugador_id: int          # el anfitrion (antes estaba fijo el jugador 1)

class AccionJugadorRequest(BaseModel):
    jugador_id: int
    partida_id: int

class AccionDijoUno(BaseModel):
    jugador_id: int
    partida_id: int
    decision: bool

class JugadaRequest(BaseModel):
    jugador_id: int
    partida_id: int
    color_carta: str
    valor_carta: str
    color_elegido: str | None = None

# Funciones de apoyo
def _get_estado(partida_id: int) -> EstadoPartida:
    estado = PARTIDAS.get(partida_id)
    if estado is None:
        raise HTTPException(
            status_code=404,
            detail="La partida no esta activa en el servidor (si reiniciaste la API hay que crear una nueva).",
        )
    return estado


def _guardar_logs(db: Session, partida_id: int, jugador_id: int, textos: list):
    for texto in textos:
        db.add(models.LogMovimiento(partida_id=partida_id, jugador_id=jugador_id, accion=texto[:255]))
    db.commit()


def _registrar_victoria_si_aplica(db: Session, estado: EstadoPartida):
    """Cuando el motor termina la partida, se guarda el ganador en MySQL (una sola vez)."""
    if estado.estado != "terminada" or estado.victoria_registrada or estado.ganador_id is None:
        return
    partida = db.get(models.Partida, estado.partida_id)
    jugador = db.get(models.Jugador, estado.ganador_id)
    if partida and jugador:
        partida.ganador_id = jugador.id
        jugador.partidas_ganadas += 1
        db.commit()
    estado.victoria_registrada = True


def _ejecutar(db: Session, partida_id: int, jugador_id: int, accion):
    """Corre una accion del motor con el candado puesto, guarda el log en MySQL
    y regresa el estado actualizado desde la perspectiva del jugador que la hizo."""
    with LOCK:
        estado = _get_estado(partida_id)
        try:
            logs = accion(estado)
        except ErrorJuego as e:
            raise HTTPException(status_code=e.codigo, detail=e.mensaje)
        _guardar_logs(db, partida_id, jugador_id, logs)
        _registrar_victoria_si_aplica(db, estado)
        return estado.a_dict(jugador_id)


def _validar_jugador(db: Session, jugador_id: int) -> models.Jugador:
    jugador = db.get(models.Jugador, jugador_id)
    if not jugador:
        raise HTTPException(status_code=404, detail="Jugador no encontrado")
    return jugador

# Jugadores y logs (sin cambios)
@app.post("/jugadores/")
def crear_jugador(jugador: JugadorCreate, db: Session = Depends(get_db)):
    db_jugador = models.Jugador(nombre=jugador.nombre)
    db.add(db_jugador)
    db.commit()
    db.refresh(db_jugador)
    return db_jugador


@app.get("/jugadores/")
def obtener_jugadores(db: Session = Depends(get_db)):
    return db.query(models.Jugador).all()


@app.post("/movimientos/")
def registrar_movimiento(movimiento: MovimientoCreate, db: Session = Depends(get_db)):
    db_mov = models.LogMovimiento(
        partida_id=movimiento.partida_id,
        jugador_id=movimiento.jugador_id,
        accion=movimiento.accion,
    )
    db.add(db_mov)
    db.commit()
    return {"mensaje": "Movimiento guardado con exito"}


@app.get("/partidas/{partida_id}/log")
def obtener_log_partida(partida_id: int, db: Session = Depends(get_db)):
    return (
        db.query(models.LogMovimiento)
        .filter(models.LogMovimiento.partida_id == partida_id)
        .all()
    )


@app.post("/jugadores/login")
def resgistrar_jugador(datos: JugadorCreate, db: Session = Depends(get_db)):
    jugador_existente = db.query(models.Jugador).filter(models.Jugador.nombre == datos.nombre).first()
    if jugador_existente:
        return {
            "id": jugador_existente.id,
            "nombre": jugador_existente.nombre,
            "partidas_ganadas": jugador_existente.partidas_ganadas,
        }

    nuevo_jugador = models.Jugador(nombre=datos.nombre, partidas_ganadas=0)
    db.add(nuevo_jugador)
    db.commit()
    db.refresh(nuevo_jugador)

    return {
        "id": nuevo_jugador.id,
        "nombre": nuevo_jugador.nombre,
        "partidas_ganadas": nuevo_jugador.partidas_ganadas,
    }

# Sala de espera: crear, unirse e iniciar
@app.post("/partida/nueva")
def iniciar_nueva_partida(datos: NuevaPartidaRequest, db: Session = Depends(get_db)):
    jugador = _validar_jugador(db, datos.jugador_id)

    nueva_partida = models.Partida()
    db.add(nueva_partida)
    db.commit()
    db.refresh(nueva_partida)

    db.add(models.JugadorPartida(jugador_id=jugador.id, partida_id=nueva_partida.id))
    db.commit()

    with LOCK:
        estado = EstadoPartida(nueva_partida.id, jugador.id, jugador.nombre)
        PARTIDAS[nueva_partida.id] = estado
        return estado.a_dict(jugador.id)


@app.post("/partida/unirse")
def unirse_a_partida(datos: AccionJugadorRequest, db: Session = Depends(get_db)):
    jugador = db.get(models.Jugador, datos.jugador_id)
    partida = db.get(models.Partida, datos.partida_id)
    if not jugador or not partida:
        raise HTTPException(status_code=404, detail="Jugador o Partida no encontrados")

    with LOCK:
        estado = _get_estado(datos.partida_id)
        try:
            es_nuevo = estado.agregar_jugador(jugador.id, jugador.nombre)
        except ErrorJuego as e:
            raise HTTPException(status_code=e.codigo, detail=e.mensaje)

        if es_nuevo:
            db.add(models.JugadorPartida(jugador_id=jugador.id, partida_id=partida.id))
            _guardar_logs(db, partida.id, jugador.id, [estado.ultimo_evento])
        return estado.a_dict(jugador.id)


@app.post("/partida/iniciar")
def iniciar_partida(datos: AccionJugadorRequest, db: Session = Depends(get_db)):
    """Reparte las cartas y arranca. Si ya estaba empezada o terminada, la reinicia (solo el anfitrion)."""
    return _ejecutar(db, datos.partida_id, datos.jugador_id, lambda e: e.iniciar(datos.jugador_id))

# NUEVO: estado global de la partida (lo consultan los clientes con polling)
@app.get("/partida/{partida_id}/estado")
def obtener_estado(partida_id: int, jugador_id: int | None = None, desde_version: int | None = None):
    """
    - jugador_id: quien pregunta. Solo a el se le manda su mano completa.
    - desde_version: la ultima version que ya tiene el cliente. Si no ha cambiado nada
      se responde algo muy pequeno ({"sin_cambios": true}) para no gastar red.
    """
    with LOCK:
        estado = _get_estado(partida_id)
        if desde_version is not None and desde_version == estado.version:
            return {"sin_cambios": True, "version": estado.version}
        return estado.a_dict(jugador_id)

# Acciones del juego: ahora el SERVIDOR valida y aplica, y regresa el estado nuevo
@app.post("/partida/jugar-carta")
def jugar_carta(datos: JugadaRequest, db: Session = Depends(get_db)):
    return _ejecutar(
        db, datos.partida_id, datos.jugador_id,
        lambda e: e.jugar(datos.jugador_id, datos.color_carta, datos.valor_carta, datos.color_elegido),
    )


@app.post("/partida/robar-carta")
def robar_carta(datos: AccionJugadorRequest, db: Session = Depends(get_db)):
    return _ejecutar(db, datos.partida_id, datos.jugador_id, lambda e: e.robar(datos.jugador_id))


@app.post("/partida/tomar-penalizacion")
def tomar_penalizacion(datos: AccionJugadorRequest, db: Session = Depends(get_db)):
    return _ejecutar(db, datos.partida_id, datos.jugador_id, lambda e: e.tomar_penalizacion(datos.jugador_id))


@app.post("/partida/pasar-turno")
def pasar_turno(datos: AccionJugadorRequest, db: Session = Depends(get_db)):
    return _ejecutar(db, datos.partida_id, datos.jugador_id, lambda e: e.pasar(datos.jugador_id))


@app.post("/partida/uno")
def decir_uno(datos: AccionDijoUno, db: Session = Depends(get_db)):
    return _ejecutar(db, datos.partida_id, datos.jugador_id, lambda e: e.decir_uno(datos.jugador_id, datos.decision))