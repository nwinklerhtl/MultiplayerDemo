using System.Text.Json;
using LiteNetLib;
using Messages;
using Microsoft.AspNetCore.SignalR;
using Server.Model;

namespace Server;

public class GameServer
{
    // infrastructure
    private readonly NetManager _net;
    private readonly EventBasedNetListener _listener;
    private readonly IHubContext<NetworkHub> _signalrHub;
    private readonly object _lock = new();
    private readonly Random _rng = new();
    
    // state
    private readonly Dictionary<string, SimPlayer> _playersSim = new();
    private readonly List<OrbDto> _orbs = new();
    
    private readonly float _worldW = 800, _worldH = 600; // match client canvas for demo
    private readonly double _tickDt = 0.025; // 25 ms tick (40 Hz) = smoother
    private DateTime _t0 = DateTime.UtcNow;
    private double NowSec => (DateTime.UtcNow - _t0).TotalSeconds;

    private string? _lastSentSignalRState = null;
    
    public GameServer(IHubContext<NetworkHub> signalrHub)
    {
        _signalrHub = signalrHub;
        _listener = new EventBasedNetListener();

        // Subscribe to relevant events
        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;

        _net = new NetManager(_listener);
        _net.Start(9050);
        
        // spawn initial orbs
        for (int i = 0; i < 6; i++) SpawnOrb();
        
        Console.WriteLine("LiteNetLib server started on port 9050");
    }

    private void OnConnectionRequest(ConnectionRequest request)
    {
        // For simplicity, accept all
        request.Accept();
    }

    private void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine($"Peer connected: {peer.Address}:{peer.Port}");
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        var existingPlayer = _playersSim.FirstOrDefault(p => p.Value.PeerId == peer.Id);
        if (!existingPlayer.Equals(default(KeyValuePair<string, SimPlayer>)))
        {
            _playersSim.Remove(existingPlayer.Key);
        } 
        Console.WriteLine($"Peer [{peer.Id}] disconnected: {peer.Address}:{peer.Port} | Reason: {disconnectInfo.Reason} | Connected Clients: {_playersSim.Count}");
    }

    private void OnNetworkReceive(NetPeer peer,
        NetPacketReader reader,
        byte channel,
        DeliveryMethod deliveryMethod)
    {
        try
        {
            var bytes = reader.GetRemainingBytes();

            // Dashboard mirror (optional)
            _ = _signalrHub.Clients.All.SendAsync("PacketEvent", new {
                Src    = $"{peer.Address}:{peer.Port}",
                Payload= System.Text.Encoding.UTF8.GetString(bytes),
                Time   = DateTime.UtcNow.ToString("HH:mm:ss.fff")
            });

            var msg = JsonSerializer.Deserialize(bytes, WireContext.Default.InputMessage);
            if (msg is null || string.IsNullOrWhiteSpace(msg.Id))
                return;

            lock (_lock)
            {
                if (!_playersSim.TryGetValue(msg.Id, out var sp))
                {
                    sp = new SimPlayer { Id = msg.Id, PeerId = peer.Id, X = _worldW*0.5f, Y = _worldH*0.5f };
                    _playersSim[msg.Id] = sp;
                    Console.WriteLine($"Created SimPlayer for id={msg.Id}");
                }

                // normalize input
                var len = MathF.Sqrt(msg.Input.Dx*msg.Input.Dx + msg.Input.Dy*msg.Input.Dy);
                if (len > 0.0001f)
                {
                    sp.InputDx = msg.Input.Dx / len;
                    sp.InputDy = msg.Input.Dy / len;
                    sp.Angle   = MathF.Atan2(sp.InputDy, sp.InputDx);
                }
                else { sp.InputDx = 0; sp.InputDy = 0; }

                if (msg.Input.Boost) sp.ConsumeBoost = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Receive error: " + ex);
        }
        finally
        {
            reader.Recycle();
        }
    }
    
    private void SimTick()
    {
        double now = NowSec;
        float dt = (float)_tickDt;

        lock (_lock)
        {
            // move players with boost
            foreach (var sp in _playersSim.Values)
            {
                // Handle boost consumption (edge-triggered)
                if (sp.ConsumeBoost && sp.BoostCharges > 0 && !sp.BoostActive)
                {
                    sp.BoostActive = true;
                    sp.BoostCharges -= 1;
                    sp.BoostUntil = now + 0.6; // 600 ms boost
                }
                sp.ConsumeBoost = false;

                if (sp.BoostActive && now >= sp.BoostUntil)
                    sp.BoostActive = false;

                float intentLen = MathF.Sqrt(sp.InputDx * sp.InputDx + sp.InputDy * sp.InputDy);
                float ndx = 0, ndy = 0;
                if (intentLen > 0.0001f)
                {
                    ndx = sp.InputDx / intentLen;
                    ndy = sp.InputDy / intentLen;
                    sp.Angle = MathF.Atan2(ndy, ndx); // face movement direction
                }

                float speed = sp.BoostActive ? sp.Speed * 2.2f : sp.Speed;
                sp.X += ndx * speed * dt;
                sp.Y += ndy * speed * dt;

                // clamp to world bounds
                sp.X = Math.Clamp(sp.X, 20f, _worldW - 20f);
                sp.Y = Math.Clamp(sp.Y, 20f, _worldH - 20f);
            }

            // collision with orbs (server-authoritative)
            const float collectR2 = 20f * 20f;
            for (int i = _orbs.Count - 1; i >= 0; i--)
            {
                var orb = _orbs[i];
                foreach (var sp in _playersSim.Values)
                {
                    var dx = sp.X - orb.X;
                    var dy = sp.Y - orb.Y;
                    if (dx * dx + dy * dy <= collectR2)
                    {
                        // collect
                        sp.Score += 1;
                        // sp.BoostCharges += 1; // award one boost
                        sp.BoostCharges = 1; // set one boost
                        _orbs.RemoveAt(i);
                        SpawnOrb();
                        break;
                    }
                }
            }
        }
    }

    public async Task TickBroadcast()
    {
        // poll events first
        _net.PollEvents();

        // simulate
        SimTick();

        // build state snapshot
        PlayerDto[] players;
        OrbDto[] orbs;
        lock (_lock)
        {
            players = _playersSim.Values.Select(p =>
                new PlayerDto(p.Id, p.X, p.Y, p.Angle, p.Score, p.BoostCharges, p.BoostActive)
            ).ToArray();

            orbs = _orbs.Select(o => new OrbDto(o.Id, o.X, o.Y)).ToArray();
        }

        var state = new StateMessage(players, orbs);
        var data  = JsonSerializer.SerializeToUtf8Bytes(state, WireContext.Default.StateMessage);

        foreach (var peer in _net.ConnectedPeerList)
            peer.Send(data, DeliveryMethod.Unreliable);

        // _ = _signalrHub.Clients.All.SendAsync("StateUpdate", new { Payload = System.Text.Encoding.UTF8.GetString(data), Time = DateTime.UtcNow.ToString("HH:mm:ss.fff") });
        var stateAsString = state.StateToString();
        if (_lastSentSignalRState is null || _lastSentSignalRState != stateAsString)
        {
            _lastSentSignalRState = stateAsString;
            await _signalrHub.Clients.All.SendAsync("StateUpdate", new SignalRStateMessage(DateTime.UtcNow, state));
        }
    }
    
    private void SpawnOrb()
    {
        var id = $"orb_{Guid.NewGuid().ToString()[..6]}";
        var x = (float)_rng.Next(40, (int)_worldW - 40);
        var y = (float)_rng.Next(40, (int)_worldH - 40);
        _orbs.Add(new OrbDto(id, x, y));
    }
}