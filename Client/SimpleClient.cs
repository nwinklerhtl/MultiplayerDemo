using System.Text.Json;
using LiteNetLib;
using Raylib_cs;

namespace Client;

public class SimpleClient
{
    private readonly NetManager _net;
    private readonly EventBasedNetListener _listener;
    private NetPeer? _serverPeer;
    private readonly string _id;
    private readonly Dictionary<string, (float x, float y, float lastX, float lastY, double lastUpdate)> _players = new();

    private readonly object _lock = new();

    public SimpleClient(string id)
    {
        _id = id;
        _listener = new EventBasedNetListener();

        // Attach event handlers
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;

        _net = new NetManager(_listener);
        _net.Start();
        _net.Connect("127.0.0.1", 9050, "demo");
    }

    private void OnPeerConnected(NetPeer peer)
    {
        _serverPeer = peer;
        Console.WriteLine($"Connected to server at {peer.Address}");
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Console.WriteLine($"Disconnected from server. Reason: {disconnectInfo.Reason}");
        _serverPeer = null;
    }

    private void OnNetworkReceive(NetPeer peer,
        NetPacketReader reader,
        byte channel,
        DeliveryMethod deliveryMethod)
    {
        var json = System.Text.Encoding.UTF8.GetString(reader.GetRemainingBytes());
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.GetProperty("Type").GetString() != "state") return;

        var now = Raylib.GetTime(); // seconds since init
        var players = doc.RootElement.GetProperty("Players");

        lock (_lock)
        {
            foreach (var el in players.EnumerateArray())
            {
                var pid = el.GetProperty("Id").GetString() ?? "x";
                var x = (float)el.GetProperty("X").GetDouble();
                var y = (float)el.GetProperty("Y").GetDouble();

                if (_players.TryGetValue(pid, out var old))
                    _players[pid] = (x, y, old.x, old.y, now);
                else
                    _players[pid] = (x, y, x, y, now);
            }
        }

        reader.Recycle();
    }

    // --- Public API ----------------------------------------------------------

    public void PollEvents() => _net.PollEvents();

    public void SendInput(float dx, float dy)
    {
        if (_serverPeer == null) return;

        var msg = new { type = "input", id = _id, input = new { dx, dy } };
        var json = JsonSerializer.Serialize(msg);
        var data = System.Text.Encoding.UTF8.GetBytes(json);
        _serverPeer.Send(data, DeliveryMethod.Unreliable);
    }

    public Dictionary<string, (float x, float y, float lastX, float lastY, double lastUpdate)> GetPlayers()
    {
        lock (_lock)
        {
            // Return a shallow copy to avoid concurrency issues
            return new Dictionary<string, (float x, float y, float lastX, float lastY, double lastUpdate)>(_players);
        }
    }
    
    public Dictionary<string, (float x, float y)> GetInterpolatedPlayers()
    {
        lock (_lock)
        {
            double now = Raylib.GetTime();
            const double interpolationDelay = 0.1; // 100 ms delay

            var result = new Dictionary<string, (float x, float y)>();
            foreach (var kv in _players)
            {
                var (x, y, lastX, lastY, lastUpdate) = kv.Value;
                double t = (now - lastUpdate) / interpolationDelay;
                t = Math.Clamp(t, 0.0, 1.0);
                var ix = (float)(lastX + (x - lastX) * t);
                var iy = (float)(lastY + (y - lastY) * t);
                result[kv.Key] = (ix, iy);
            }
            return result;
        }
    }

}