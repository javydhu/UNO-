#Es lo que nos ayuda a crear la conexion
from sqlalchemy import create_engine

#Es la clase que nos ayuda a poner en codigo las bases
#relacionales de la cual van a heredar los modelos
from sqlalchemy.ext.declarative import declarative_base
#Es la que nos ayuda a hacer las sesiones mediante las cuales
#la API se conecta a la base de datos
from sqlalchemy.orm import sessionmaker

#Url que ocupamos para poder hacer la conexion con: 
#driver: mysql+pymysql
#usuario: root
#password: jaro
#servidor: localhost
#base de datos: juego_uno_db
SQLALCHEMY_DATABASE_URL = "mysql+pymysql://root:jaro3832@localhost/juego_uno_db"

#encinde el motor con la conexion que le acabamos de dar
#cuando corremos la api es la conexion que existe con la 

engine = create_engine(SQLALCHEMY_DATABASE_URL)

#Es la que nos ayuda a hacer las sesiones cuando las solicitan
#los agumentos son las caracteristicas de las sesiones
#autocommit y autoFlush es para tener un control de cuando se guardan las cosas
#bind = engine es la que vincula las sesiones que se crean con el motor que hicimos
#o con la conexion que hicmos a la base de datos
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

#es una variable que se incializa que es una de tipo de base de datos declarativa
#que nos va ayudar a las queries
Base = declarative_base()

#La funcion get_db cada que se invoca llama a nuestro
#generador de sesiones y hace una sesion, 

def get_db():
    db = SessionLocal()
    try:
        # yield es como un return, pero regresa la conexion y cunado se termina
        #de usar se regresa aqui y hace el finally y cierra la base de datos
        yield db
        #Cuando la funcion que llamo mediante un parametro a esta conexion deja de existir 
        #se regresa aqui a cerrar la base de datos
    finally:
        db.close()