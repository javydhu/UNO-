using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Uno.Shared.Models;

namespace Uno.Server.Networking;

public class ClientConnection : IDisposable
{
    private readonly TcpClient _client;

    private readonly StreamReader _reader;

    private readonly StreamWriter _writer;

    //Gracias a esto no se pueden escribir dos mensajes al mismo tiempo 
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public ClientConnection(TcpClient client)
    {
        _client = client;

        NetworkStream stream = client.GetStream();

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

    // Mensajes recibidos

    public async Task<NetworkMessage?> ReadMessageAsync()
    {
        string? line = await _reader.ReadLineAsync();

        if (string.IsNullOrWhiteSpace(line))
            return null;

        return JsonSerializer.Deserialize<NetworkMessage>(
            line,
            NetworkMessage.SerializerOptions);
    }

    //Envio de mensajes

    public async Task SendMessageAsync<T>(
        string type,
        T data)
    {
        NetworkMessage message = NetworkMessage.Create(
            type,
            data);

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

    // Creacion de conexion

    public void Dispose()
    {
        _writer.Dispose();

        _reader.Dispose();

        _client.Dispose();

        _sendLock.Dispose();
    }
}