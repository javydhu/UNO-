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

class JugadaRequest(BaseModel):
    jugador_id: int
    partida_id: int
    color_carta: str
    valor_carta: str
    color_elegido: str | None = None

#Ahora siguen los endpoints


@app.post("/jugadores/") #Se le indica donde esta el endpoint
#se crea una funcion que va a recibir lo que se le haga de peticion
#Lo que se hace es que por ejemplo jugador es un objeto de la clase
#que se acaba de crear arriba, como hereda de BaseModel, entoces
#cada que se hace un objeto de esta clase checa que el JSON que se le
#manda concuerde con los datos que ocupa la clase
def crear_jugador(jugador: JugadorCreate, db: Session = Depends(get_db)):
    #se igualan los valores de las cosas
  db_jugador = models.Jugador(nombre=jugador.nombre)
  #Se agrega como nuevo dato de la tabla
  db.add(db_jugador)
  #Se guarda como nuevo dato de la tabla
  db.commit()
  #se hace para poder tener el dato mas reciente de ese 
  #jugador
  db.refresh(db_jugador)
  #se regresa el jugador 
  return db_jugador


@app.get("/jugadores/")
def obtener_jugadores(db: Session = Depends(get_db)):
  return db.query(models.Jugador).all()
  
# La variable db que es una sesion que se la va a pedir con una
#dependencia con lo que hicimos en la base de datos de get_db
#es la que nos ayuda a hacer las cosas como los commit, query o add
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
  
@app.post("/jugadores/login")
def resgistrar_jugador(datos: JugadorCreate, db: Session = Depends(get_db)):
    jugador_existente = db.query(models.Jugador).filter(models.Jugador.nombre == datos.nombre).first()
    if jugador_existente:
        # Retornamos un diccionario plano en lugar del objeto SQLAlchemy
        return {
            "id": jugador_existente.id,
            "nombre": jugador_existente.nombre,
            "partidas_ganadas": jugador_existente.partidas_ganadas
        }
    
    nuevo_jugador = models.Jugador(nombre=datos.nombre, partidas_ganadas=0)
    db.add(nuevo_jugador)
    db.commit()
    db.refresh(nuevo_jugador)
   
    return {
        "id": nuevo_jugador.id,
        "nombre": nuevo_jugador.nombre,
        "partidas_ganadas": nuevo_jugador.partidas_ganadas
    }
  
@app.post("/partida/robar-carta")
def registrar_robo(datos: AccionJugadorRequest, db: Session = Depends(get_db)):
    jugador = db.query(models.Jugador).filter(models.Jugador.id == datos.jugador_id).first()
    if not jugador:
        return {"error": "Jugador no encontrado"}
    
    # 1. Creamos y guardamos el registro en la base de datos
    db_mov = models.LogMovimiento(
        partida_id=datos.partida_id,  
        jugador_id=datos.jugador_id,
        accion=f"El jugador {jugador.nombre} robó una carta."
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

    if datos.color_elegido is not None:
       accion_texto = f"El jugador {jugador.nombre} jugó comodin {datos.valor_carta} y cambio el color a {datos.color_elegido}"
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

    return {
        "mensaje": "Victoria registrada con éxito", 
        "partidas_ganadas": jugador.partidas_ganadas
    }

@app.get("/partida/nueva")
def iniciar_nueva_partida(db: Session = Depends(get_db)):
    # Buscamos el ID de partida más alto que exista en el log
    ultimo_id = db.query(func.max(models.LogMovimiento.partida_id)).scalar()
    
    nuevo_partida_id = (ultimo_id or 0) + 1
    
    return {"partida_id": nuevo_partida_id}