using System.Text.Json;
using LiteNetLib;
using Microsoft.AspNetCore.SignalR;
using Server.Model;

namespace Server;

public class GameServer
{
    private readonly NetManager _net;
    private readonly EventBasedNetListener _listener;
    private readonly Dictionary<string, PlayerState> _players = new();
    private readonly IHubContext<NetworkHub> _hub;
    private readonly object _lock = new();

    public GameServer(IHubContext<NetworkHub> hub)
    {
        _hub = hub;
        _listener = new EventBasedNetListener();

        // Subscribe to relevant events
        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;

        _net = new NetManager(_listener);
        _net.Start(9050);
        Console.WriteLine("LiteNetLib server started on port 9050");
    }

    private void OnConnectionRequest(ConnectionRequest request)
    {
        // For simplicity, accept all
        request.Accept();
    }

    private void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine($"Peer connected: {peer.Address}");
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Console.WriteLine($"Peer disconnected: {peer.Address} | Reason: {disconnectInfo.Reason} | Connected Clients: {_players.Count}");
    }

    private void OnNetworkReceive(NetPeer peer,
        NetPacketReader reader,
        byte channel,
        DeliveryMethod deliveryMethod)
    {
        try
        {
            var data = reader.GetRemainingBytes();
            var json = System.Text.Encoding.UTF8.GetString(data);
            // forward to dashboard
            _hub.Clients.All.SendAsync(
                "PacketEvent", 
                new
                {
                    Src = peer.Address.ToString(), 
                    Payload = json, 
                    Time = DateTime.UtcNow.ToString("HH:mm:ss.fff")
                });

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "input")
            {
                var id  = doc.RootElement.GetProperty("id").GetString() ?? "unknown";
                var dx  = (float)doc.RootElement.GetProperty("input").GetProperty("dx").GetDouble();
                var dy  = (float)doc.RootElement.GetProperty("input").GetProperty("dy").GetDouble();

                lock (_lock)
                {
                    if (!_players.ContainsKey(id))
                    {
                        _players[id] = new PlayerState(id, 100, 100);
                    }

                    var ps = _players[id];
                    float nx = ps.X + dx * 5f; // simple movement scale
                    float ny = ps.Y + dy * 5f;
                    _players[id] = new PlayerState(id, nx, ny);
                }
            }

            // Important: recycle the reader if needed
            reader.Recycle();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Receive error: " + ex);
        }
    }

    public void TickBroadcast()
    {
        // Poll events so callbacks fire
        _net.PollEvents();

        PlayerState[] snapshot;
        lock (_lock)
        {
            snapshot = _players.Values.ToArray();
        }

        var state    = new StateMessage("state", snapshot);
        var json     = JsonSerializer.Serialize(state);
        var data     = System.Text.Encoding.UTF8.GetBytes(json);
        var peers    = _net.ConnectedPeerList;

        foreach (var peer in peers)
        {
            peer.Send(data, DeliveryMethod.Unreliable);
        }

        // push to dashboard as well
        _hub.Clients.All.SendAsync(
            "StateUpdate", 
            new
            {
                Payload = json, 
                Time = DateTime.UtcNow.ToString("HH:mm:ss.fff")
            });
    }
}