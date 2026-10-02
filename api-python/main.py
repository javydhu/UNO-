from database import engine, get_db
from fastapi import Depends, FastAPI, HTTPException
import models
from pydantic import BaseModel
from sqlalchemy.orm import Session

models.Base.metadata.create_all(bind=engine)

app = FastAPI(title="UNO Game API")


class JugadorCreate(BaseModel):
  nombre: str


class MovimientoCreate(BaseModel):
  partida_id: int
  jugador_id: int
  accion: str


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