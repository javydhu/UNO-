from database import Base #Se importa la base que es una clase que extiende de base de datos relacional
from sqlalchemy import Column, Integer, String #Se integran los datos para poder usarlos como 
#tipos de datos en el codigo

#Se crea la tabla jugador como una clase, con su nombre
class Jugador(Base):
  __tablename__ = "jugadores"
  
#atributos de la case jugador, los cuales con el ID que se ponen con los atributos necesarios
  id = Column(Integer, primary_key=True, index=True, autoincrement=True)
  nombre = Column(String(100), nullable=False)


class LogMovimiento(Base):
  __tablename__ = "log_movimientos"

  id = Column(Integer, primary_key=True, index=True, autoincrement=True)
  partida_id = Column(Integer, nullable=False)
  jugador_id = Column(Integer, nullable=False)
  accion = Column(String(255), nullable=False)