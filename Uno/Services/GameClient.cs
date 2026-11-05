using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Uno.Shared.Models;

namespace Uno.Services;

public class GameClient : IDisposable
{
    private readonly TcpClient _tcpClient = new();

    private StreamReader? _reader;
    private StreamWriter? _writer;

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public bool IsConnected =>
        _tcpClient.Connected;

    // Conectar

    public async Task ConnectAsync(
        string ip,
        int port,
        CancellationToken cancellationToken = default)
    {
        await _tcpClient
            .ConnectAsync(ip, port)
            .WaitAsync(cancellationToken);

        _tcpClient.NoDelay = true;

        NetworkStream stream =
            _tcpClient.GetStream();

        _reader = new StreamReader(
            stream,
            Encoding.UTF8);

        _writer = new StreamWriter(
            stream,
            new UTF8Encoding(false))
        {
            AutoFlush = true
        };
    }

    // Enviar mensaje

    public async Task SendMessageAsync<T>(
        string type,
        T data)
    {
        if (_writer == null)
            throw new InvalidOperationException(
                "No existe una conexión con el servidor.");

        NetworkMessage message =
            NetworkMessage.Create(type, data);

        string json = JsonSerializer.Serialize(
            message,
            NetworkMessage.SerializerOptions);

        await _sendLock.WaitAsync();

        try
        {
            await _writer.WriteLineAsync(json);

            await _writer.FlushAsync();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    // Recibir mensaje

    public async Task<NetworkMessage?> ReadMessageAsync(
        CancellationToken cancellationToken = default)
    {
        if (_reader == null)
            throw new InvalidOperationException(
                "No existe una conexión con el servidor.");

        string? line = await _reader
            .ReadLineAsync()
            .WaitAsync(cancellationToken);

        if (line == null)
            return null;

        if (string.IsNullOrWhiteSpace(line))
            return null;

        return JsonSerializer.Deserialize<NetworkMessage>(
            line,
            NetworkMessage.SerializerOptions);
    }

    // Cerrar conexion

    public void Dispose()
    {
        _tcpClient.Dispose();
        _sendLock.Dispose();
    }
}