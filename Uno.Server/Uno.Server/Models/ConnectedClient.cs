using Uno.Server.Networking;
using Uno.Shared.Models;

namespace Uno.Server.Models;

public class ConnectedClient
{
    public ClientConnection Connection { get; }

    public Player Player { get; }

    public bool IsConnected { get; set; } = true;

    public ConnectedClient(
        ClientConnection connection,
        Player player)
    {
        Connection = connection;
        Player = player;
    }
}