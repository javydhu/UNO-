using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Uno.Server.Services;

namespace Uno.Server.Networking;

public class TcpServer
{
    private readonly TcpListener _listener;

    private readonly GameManager _gameManager;

    public TcpServer(GameManager gameManager, int port)
    {
        _gameManager = gameManager;

        _listener = new TcpListener(
            IPAddress.Any,
            port);
    }

    // Iniciar el servidor
    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        _listener.Start();

        Console.WriteLine(
            "================================");

        Console.WriteLine(
            "       SERVIDOR UNO INICIADO");

        Console.WriteLine(
            "================================");

        Console.WriteLine(
            $"Escuchando en el puerto {_listener.LocalEndpoint}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient tcpClient;

                try
                {
                    // Esperar una nueva conexión.
                    tcpClient = await _listener.AcceptTcpClientAsync();
                }
                catch (SocketException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Console.WriteLine(
                    $"Nueva conexión desde: {tcpClient.Client.RemoteEndPoint}");
                
                _ = HandleClientAsync(
                    tcpClient,
                    cancellationToken);
            }
        }
        finally
        {
            _listener.Stop();

            Console.WriteLine(
                "Servidor detenido.");
        }
    }

    //Se atiende a un cliente

    private async Task HandleClientAsync(
        TcpClient tcpClient,
        CancellationToken cancellationToken)
    {
        using ClientConnection connection =
            new ClientConnection(tcpClient);

        try
        {
            await _gameManager.HandleClientAsync(
                connection,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Error atendiendo conexión: {ex.Message}");
        }
    }

    //Se detiene el servidor

    public void Stop()
    {
        _listener.Stop();
    }
}