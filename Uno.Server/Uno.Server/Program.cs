using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Uno.Server.Networking;
using Uno.Server.Services;



const int PORT = 5000;

const int PLAYERS_TO_START = 3;



string pythonUrl =
    Environment.GetEnvironmentVariable("UNO_PYTHON_API_URL")
    ?? "http://127.0.0.1:8000/";

using HttpClient httpClient = new HttpClient
{
    BaseAddress = new Uri(pythonUrl),
    Timeout = TimeSpan.FromSeconds(8)
};

PythonApiService pythonApi =
    new PythonApiService(httpClient);


GameManager gameManager =
    new GameManager(
        pythonApi,
        PLAYERS_TO_START);


TcpServer server =
    new TcpServer(
        gameManager,
        PORT);

//Mostrar ip locales

Console.WriteLine("Direcciones IPv4 disponibles:");

foreach (NetworkInterface networkInterface
         in NetworkInterface.GetAllNetworkInterfaces())
{
    if (networkInterface.OperationalStatus !=
        OperationalStatus.Up)
        continue;

    foreach (UnicastIPAddressInformation address
             in networkInterface.GetIPProperties().UnicastAddresses)
    {
        if (address.Address.AddressFamily ==
            AddressFamily.InterNetwork &&
            !IPAddress.IsLoopback(address.Address))
        {
            Console.WriteLine(address.Address);
        }
    }
}


using CancellationTokenSource cts = new();

Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;

    Console.WriteLine("\nCerrando servidor...");

    cts.Cancel();

    server.Stop();
};

// Iniciar

try
{
    await server.RunAsync(cts.Token);
}
catch (Exception ex)
{
    Console.WriteLine(
        $"Error iniciando servidor: {ex.Message}");
}