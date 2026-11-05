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

    public TcpServer(
        GameManager gameManager,
        int port)
    {
        _gameManager = gameManager;

        _listener = new TcpListener(
            IPAddress.Any,
            port);
    }

    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        _listener.Start();

        Console.WriteLine("================================");
        Console.WriteLine("       SERVIDOR UNO INICIADO");
        Console.WriteLine("================================");

        Console.WriteLine(
            $"Puerto: {((IPEndPoint)_listener.LocalEndpoint).Port}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient tcpClient;

                try
                {
                    tcpClient = await _listener
                        .AcceptTcpClientAsync()
                        .WaitAsync(cancellationToken);

                    tcpClient.NoDelay = true;
                }
                catch (OperationCanceledException)
                {
                    break;
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
                    $"Conexión desde {tcpClient.Client.RemoteEndPoint}");

                _ = HandleClientAsync(
                    tcpClient,
                    cancellationToken);
            }
        }
        finally
        {
            _listener.Stop();

            Console.WriteLine("Servidor detenido.");
        }
    }

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
                $"Error de cliente: {ex.Message}");
        }
    }

    public void Stop()
    {
        _listener.Stop();
    }
}