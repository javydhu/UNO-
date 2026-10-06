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