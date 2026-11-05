using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Uno.Shared.Models; 

namespace Uno.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== SERVIDOR DE UNO INICIADO ===");
            TcpListener listener = new TcpListener(IPAddress.Any, 9000);
            listener.Start();
            Console.WriteLine("Esperando conexiones de jugadores en el puerto 9000...");

            // Instanciamos el juego central con sus reglas y baraja
            // (Puedes pasarle los jugadores reales conforme se vayan conectando)
            UnoGame juego = new UnoGame();

            while (true)
            {
                // Espera a que un cliente (jugador) abra su aplicación y se conecte
                TcpClient cliente = await listener.AcceptTcpClientAsync();
                Console.WriteLine("¡Un jugador se ha conectado al servidor!");

                // Atendemos la comunicación de este cliente en segundo plano
                _ = ManejarClienteAsync(cliente, juego);
            }
        }

        private static async Task ManejarClienteAsync(TcpClient cliente, UnoGame juego)
        {
            NetworkStream stream = cliente.GetStream();
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int bytesLeidos = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesLeidos == 0) break;

                    string mensajeJson = Encoding.UTF8.GetString(buffer, 0, bytesLeidos);
                    Console.WriteLine($"Mensaje recibido del cliente: {mensajeJson}");

                    // Aquí procesaremos las acciones del cliente (tirar carta, robar, etc.)
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error de conexión con el cliente: {ex.Message}");
            }
            finally
            {
                cliente.Close();
                Console.WriteLine("Cliente desconectado.");
            }
        }
    }
}