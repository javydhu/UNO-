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

#Se utliza para crear los moldes de los datos, es el que ayuda
#a revisar que los datos que mando C# como peticion en JSON
#son los correctos para poder hacer la query
from pydantic import BaseModel

#Es la libreria que tiene los tipos de datos que vamos a
#ocupar durante la peticion
from sqlalchemy.orm import Session

#Esta libreria nos ayuda a hacer funciones en la tabla
from sqlalchemy import func

#Que se creen en la base de datos en caso de que no esten
models.Base.metadata.create_all(bind=engine)

app = FastAPI(title="UNO Game API")

#Son modelos heredados de Pydantic de BaseModel
#Se encarga que las peticiones que se hagan sean correctas
#por ejemplo si la primera tiene un numero enves de string 
#marcaria error
class JugadorCreate(BaseModel):
  nombre: str

class MovimientoCreate(BaseModel):
  partida_id: int
  jugador_id: int
  accion: str
  

class AccionJugadorRequest(BaseModel):
    jugador_id: int
    partida_id: int

class AccionDijoUno(BaseModel):
    jugador_id: int
    partida_id: int
    decision: bool

class JugadorRoba(BaseModel):
    jugador_id: int
    partida_id: int
    color_carta: str
    valor_carta: str

class JugadaRequest(BaseModel):
    jugador_id: int
    partida_id: int
    color_carta: str
    valor_carta: str
    color_elegido: str | None = None

#Ahora siguen los endpoints

def obtener_o_crear_jugador(nombre: str, db: Session):

    nombre = nombre.strip()

    if not nombre:
        raise HTTPException(
            status_code=400,
            detail="El nombre no puede estar vacío."
        )

    # Buscar ignorando mayúsculas, minúsculas
    jugador = (
        db.query(models.Jugador)
        .filter(
            func.lower(func.trim(models.Jugador.nombre))
            == nombre.lower()
        )
        .first()
    )

    # Si ya existe, devolverlo.
    if jugador:
        return {
            "id": jugador.id,
            "nombre": jugador.nombre,
            "partidas_ganadas": jugador.partidas_ganadas
        }

    # Si no existe, crearlo.
    nuevo_jugador = models.Jugador(
        nombre=nombre,
        partidas_ganadas=0
    )

    db.add(nuevo_jugador)
    db.commit()
    db.refresh(nuevo_jugador)

    return {
        "id": nuevo_jugador.id,
        "nombre": nuevo_jugador.nombre,
        "partidas_ganadas": nuevo_jugador.partidas_ganadas
    }


# Crear o recuperar un jugador.

@app.post("/jugadores/")
def crear_jugador(
    datos: JugadorCreate,
    db: Session = Depends(get_db)
):
    return obtener_o_crear_jugador(datos.nombre, db)


@app.post("/jugadores/login")
def registrar_jugador(
    datos: JugadorCreate,
    db: Session = Depends(get_db)
):
    return obtener_o_crear_jugador(datos.nombre, db)


# Consultar todos los jugadores registrados.

@app.get("/jugadores/")
def obtener_jugadores(
    db: Session = Depends(get_db)
):
    jugadores = (
        db.query(models.Jugador)
        .order_by(models.Jugador.nombre)
        .all()
    )

    return [
        {
            "id": jugador.id,
            "nombre": jugador.nombre,
            "partidas_ganadas": jugador.partidas_ganadas
        }
        for jugador in jugadores
    ]
    
@app.post("/movimientos/")
def registrar_movimiento(
    movimiento: MovimientoCreate, db: Session = Depends(get_db)
):
  db_mov = models.LogMovimiento(
      partida_id=movimiento.partida_id,
      jugador_id=movimiento.jugador_id,
      accion=movimiento.accion,
  )
  db.add(db_mov)
  db.commit()
  return {"mensaje": "Movimiento guardado con éxito"}


@app.get("/partidas/{partida_id}/log")
def obtener_log_partida(partida_id: int, db: Session = Depends(get_db)):
  return (
      db.query(models.LogMovimiento)
      .filter(models.LogMovimiento.partida_id == partida_id)
      .all()
  )
 
  
@app.post("/partida/robar-carta")
def registrar_robo(datos: JugadorRoba, db: Session = Depends(get_db)):
    jugador = db.query(models.Jugador).filter(models.Jugador.id == datos.jugador_id).first()
    if not jugador:
        return {"error": "Jugador no encontrado"}
    
    # 1. Creamos y guardamos el registro en la base de datos
    db_mov = models.LogMovimiento(
        partida_id=datos.partida_id,  
        jugador_id=datos.jugador_id,
        accion=f"El jugador {jugador.nombre} robó la tarjeta {datos.valor_carta} {datos.color_carta}"
    )
    db.add(db_mov)
    db.commit()

    return {"mensaje": "Robo registrado con éxito"}
    

@app.post("/partida/tomar-penalizacion")
def registrar_penalizacion(datos: AccionJugadorRequest, db: Session = Depends(get_db)):
    jugador = db.query(models.Jugador).filter(models.Jugador.id == datos.jugador_id).first()
    if not jugador:
        return {"error": "Jugador no encontrado"}

    # Guardamos la penalización en el log
    db_mov = models.LogMovimiento(
        partida_id=datos.partida_id,
        jugador_id=datos.jugador_id,
        accion=f"El jugador {jugador.nombre} tomó penalización."
    )
    db.add(db_mov)
    db.commit()

    return {"mensaje": "Penalización registrada con éxito"}
    

@app.post("/partida/pasar-turno")
def registrar_pase_turno(datos: AccionJugadorRequest, db: Session = Depends(get_db)):
    jugador = db.query(models.Jugador).filter(models.Jugador.id == datos.jugador_id).first()
    if not jugador:
        return {"error": "Jugador no encontrado"}

    # Guardamos el pase de turno en el log
    db_mov = models.LogMovimiento(
        partida_id=datos.partida_id,
        jugador_id=datos.jugador_id,
        accion=f"El jugador {jugador.nombre} pasó su turno."
    )
    db.add(db_mov)
    db.commit()

    return {"mensaje": "Turno pasado con éxito"}

@app.post("/partida/jugar-carta")
def registrar_jugada(datos: JugadaRequest, db: Session = Depends(get_db)):
    jugador = db.query(models.Jugador).filter(models.Jugador.id == datos.jugador_id).first()
    if not jugador:
        return {"error": "Jugador no encontrado"}

    if datos.color_carta == "Wild":
       accion_texto = f"El jugador {jugador.nombre} jugó {datos.valor_carta} y cambio el color a {datos.color_elegido}"
    else:
        accion_texto = f"El jugador {jugador.nombre} jugó {datos.color_carta} {datos.valor_carta}."

    db_mov = models.LogMovimiento(
        partida_id=datos.partida_id,  
        jugador_id=datos.jugador_id,
        accion=accion_texto
    )
    db.add(db_mov)
    db.commit()

    return {"mensaje": "Jugada registrada con éxito"}


@app.post("/partida/gano")
def registrar_victoria(datos: AccionJugadorRequest, db: Session = Depends(get_db)):
    jugador = db.query(models.Jugador).filter(models.Jugador.id == datos.jugador_id).first()
    if not jugador:
        return {"error": "Jugador no encontrado"}
    
    partida = db.query(models.Partida).filter(models.Partida.id == datos.partida_id).first()
    if not partida:
        return {"error": "Partida no encontrada"}
        
    partida.ganador_id = datos.jugador_id
    
    if partida.ganador_id is not None:
    
        if partida.ganador_id == jugador.id:
            return {
                "mensaje": "La victoria ya había sido registrada."
            }
    
        raise HTTPException(
            status_code=409,
            detail="Esta partida ya tiene un ganador."
        )
    jugador.partidas_ganadas += 1
    
    db_mov = models.LogMovimiento(
        partida_id=datos.partida_id,  
        jugador_id=datos.jugador_id,
        accion=f"¡El jugador {jugador.nombre} ha ganado la partida!"
    )
    db.add(db_mov)
    
    # 4. Guardamos los cambios en la base de datos
    db.commit()
    db.refresh(jugador)
    db.refresh(partida)

    return {
           "mensaje": "Victoria registrada con éxito", 
           "partida_id": partida.id,
           "ganador_id": partida.ganador_id,
           "partidas ganadas": jugador.partidas_ganadas
    } 

@app.post("/partida/nueva")
def iniciar_nueva_partida(
    db: Session = Depends(get_db)
):

    nueva_partida = models.Partida()

    db.add(nueva_partida)
    db.commit()
    db.refresh(nueva_partida)

    return {
        "partida_id": nueva_partida.id
    }

@app.post("/partida/unirse")
def unirse_a_partida(
    datos: AccionJugadorRequest,
    db: Session = Depends(get_db)
):
    # Existe el jugador.
    jugador = (
        db.query(models.Jugador)
        .filter(models.Jugador.id == datos.jugador_id)
        .first()
    )
    # Comprobar que existe la partida.
    partida = (
        db.query(models.Partida)
        .filter(models.Partida.id == datos.partida_id)
        .first()
    )
    if not jugador or not partida:
        raise HTTPException(
            status_code=404,
            detail="Jugador o partida no encontrados."
        )
    vinculo = (
        db.query(models.JugadorPartida)
        .filter(
            models.JugadorPartida.jugador_id == datos.jugador_id,
            models.JugadorPartida.partida_id == datos.partida_id
        )
        .first()
    )
    if vinculo:
        return {
            "mensaje": "El jugador ya pertenece a esta partida."
        }
    # Crear asociación.
    nuevo_vinculo = models.JugadorPartida(
        jugador_id=datos.jugador_id,
        partida_id=datos.partida_id
    )
    db.add(nuevo_vinculo)
    db.commit()
    return {
        "mensaje": f"El jugador {jugador.nombre} se unió a la partida {partida.id}"
    }

@app.post("/partida/uno")
def registrar_uno(datos: AccionDijoUno, db: Session = Depends(get_db)):
    jugador = db.query(models.Jugador).filter(models.Jugador.id == datos.jugador_id).first()
    if not jugador:
        return {"error": "Jugador no encontrado"}

    if datos.decision == True:
        db_mov = models.LogMovimiento(
            partida_id=datos.partida_id,  
            jugador_id=datos.jugador_id,
            accion=f"¡{jugador.nombre} dijo UNO!"
        )
    else:
        db_mov = models.LogMovimiento(
            partida_id=datos.partida_id,  
            jugador_id=datos.jugador_id,
            accion=f"¡{jugador.nombre} no dijo UNO!"
        )
    db.add(db_mov)
    
    #Guardamos los cambios en la base de datos
    db.commit()
    db.refresh(jugador)

    return {
        "mensaje": "Un jugador tiene una carta"
    }
    
@app.post("/partida/sin-cartas")
def sin_cartas(datos: AccionJugadorRequest, db: Session = Depends(get_db)):
    jugador = db.query(models.Jugador).filter(models.Jugador.id == datos.jugador_id).first()
    if not jugador:
        return {"error": "Jugador no encontrado"}
        
    db_mov = models.LogMovimiento(
        partida_id=datos.partida_id,  
        jugador_id=datos.jugador_id,
        accion=f"¡El jugador {jugador.nombre} se acabo el mazo"
    )
    db.add(db_mov)
    
    db.commit()
    db.refresh(jugador)

    return {
        "mensaje": "El mazo se termino", 
    }