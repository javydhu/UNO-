from database import Base #Se importa la base que es una clase que extiende de base de datos relacional
from sqlalchemy import Column, Integer, String, ForeignKey
#Se integran los datos para poder usarlos como 
#tipos de datos en el codigo
from sqlalchemy.orm import relationship

#Se crea la tabla jugador como una clase, con su nombre
class Jugador(Base):
  __tablename__ = "jugadores"
#atributos de la case jugador, los cuales con el ID que se ponen con los atributos necesarios
  id = Column(Integer, primary_key=True, index=True, autoincrement=True)
  nombre = Column(String(100), nullable=False)
  partidas_ganadas = Column(Integer, default = 0, nullable = False)
  
  #Relaciones
  movimientos = relationship("LogMovimiento", back_populates="jugador")
  partidas_ganadas_rel = relationship("Partida", foreign_keys="Partida.ganador_id", back_populates="ganador")
  participaciones = relationship("JugadorPartida", back_populates="jugador")

class LogMovimiento(Base):
  __tablename__ = "log_movimientos"

  id = Column(Integer, primary_key=True, index=True, autoincrement=True)
  partida_id = Column(Integer, ForeignKey("partidas.id"))
  jugador_id = Column(Integer, ForeignKey("jugadores.id"))
  accion = Column(String(255), nullable=False)
  
  jugador = relationship("Jugador", back_populates="movimientos")
  partida = relationship("Partida", back_populates="movimientos")
  
class Partida(Base):
    __tablename__ = "partidas"
    
    id = Column(Integer, primary_key=True, index=True, autoincrement=True)
    ganador_id = Column(Integer, ForeignKey("jugadores.id"), nullable=True)
    
    ganador = relationship("Jugador", foreign_keys=[ganador_id], back_populates="partidas_ganadas_rel")
    movimientos = relationship("LogMovimiento", back_populates="partida")
    jugadores = relationship("JugadorPartida", back_populates="partida")

class JugadorPartida(Base):
    __tablename__ = "jugador_partida"
    id = Column(Integer, primary_key=True, index=True, autoincrement=True)
    jugador_id = Column(Integer, ForeignKey("jugadores.id"), nullable=False)
    partida_id = Column(Integer, ForeignKey("partidas.id"), nullable=False)
    
    jugador = relationship("Jugador", back_populates="participaciones")
    partida = relationship("Partida", back_populates="jugadores")
