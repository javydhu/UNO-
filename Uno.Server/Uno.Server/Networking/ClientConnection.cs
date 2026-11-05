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

    public async Task<NetworkMessage?> ReadMessageAsync(
        CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;

            try
            {
                line = await _reader.ReadLineAsync()
                    .WaitAsync(cancellationToken);
            }
            catch (IOException)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }

            if (line == null)
                return null;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            return JsonSerializer.Deserialize<NetworkMessage>(
                line,
                NetworkMessage.SerializerOptions);
        }

        return null;
    }

    public async Task SendMessageAsync<T>(
        string type,
        T data,
        CancellationToken cancellationToken = default)
    {
        NetworkMessage message = NetworkMessage.Create(
            type,
            data);

        string json = JsonSerializer.Serialize(
            message,
            NetworkMessage.SerializerOptions);

        await _sendLock.WaitAsync(cancellationToken);

        try
        {
            await _writer.WriteLineAsync(json);
            await _writer.FlushAsync(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _sendLock.Dispose();
    }
}