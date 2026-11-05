using System;
using System.Threading;
using System.Threading.Tasks;
using Uno.Server.Networking;
using Uno.Server.Services;

// Configuracion de cuantos jugadores se necesitan y que puerto se utilizara

const int PORT = 5000;

const int PLAYERS_TO_START = 3;

// Creacion de los servicios 


GameManager gameManager = new GameManager(
    PLAYERS_TO_START);

TcpServer server = new TcpServer(
    gameManager,
    PORT);


// Control de cierre

using CancellationTokenSource cancellationTokenSource = new();

Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;

    Console.WriteLine(
        "\nCerrando servidor...");

    cancellationTokenSource.Cancel();

    server.Stop();
};


// Iniciar servidor
try
{
    await server.RunAsync(
        cancellationTokenSource.Token);
}
catch (Exception ex)
{
    Console.WriteLine(
        $"Error en el servidor: {ex.Message}");
}