using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Client.Model;
using LiteNetLib;
using Messages;
using Raylib_cs;

namespace Client;

public class SimpleClient
{
    // infrastructure
    private readonly NetManager _net;
    private readonly EventBasedNetListener _listener;
    private NetPeer? _serverPeer;
    private readonly object _lock = new();
    
    // state
    private readonly string _id;
    private readonly Dictionary<string, PlayerView> _views = new();
    private readonly List<(float x,float y)> _orbsClient = new();

    private bool playerWasBoosting = false;
    private int playerLastOrbCount = 0;

    private bool _gameOver = false;
    private bool _gameWon = false;
    
    public bool GameOver => _gameOver;
    public bool GameWon => _gameWon;

    public List<(float x, float y)> OrbsClient => _orbsClient;
    
    // events
    public event EventHandler UsedBoost;
    public event EventHandler CollectedOrb;


    public SimpleClient(string id, IPAddress serverIp)
    {
        _id = id;
        _listener = new EventBasedNetListener();

        // Attach event handlers
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;

        _net = new NetManager(_listener);
        _net.Start();
        _net.Connect(serverIp.ToString(), 9050, "demo");
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
        try
        {
            var bytes = reader.GetRemainingBytes();

            var env = JsonSerializer.Deserialize<Envelope<JsonElement>>(bytes);

            switch (env?.Type)
            {
                case MessageType.State:
                    var state = env.Payload.Deserialize(WireContext.Default.StateMessage);
                    HandleState(state!);
                    break;

                case MessageType.GameOver:
                    var go = env.Payload.Deserialize(WireContext.Default.GameOverDto);
                    HandleGameOver(go!);
                    break;

                case MessageType.Reset:
                    HandleReset();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("NetworkReceive error: " + ex.Message);
        }
        finally
        {
            reader.Recycle();
        }
        
        
    }

    private void HandleState(StateMessage state)
    {
        // derive events
        var player = state.Players.FirstOrDefault(p => p.Id == _id);
        if (player is not null)
        {
            if (!player.BoostActive)
            {
                playerWasBoosting = false;
            }
            
            if (!playerWasBoosting && player.BoostActive)
            {
                playerWasBoosting = true;
                UsedBoost?.Invoke(this, EventArgs.Empty);
            }

            if (playerLastOrbCount < player.Score)
            {
                playerLastOrbCount = player.Score;
                CollectedOrb?.Invoke(this, EventArgs.Empty);
            }
        }

        var now = Raylib.GetTime();
        lock (_lock)
        {
            // update players
            foreach (var p in state.Players)
            {
                if (!_views.TryGetValue(p.Id, out var pv))
                {
                    pv = new PlayerView { Id = p.Id, X = p.X, Y = p.Y, LastX = p.X, LastY = p.Y, Angle = p.Angle, LastAngle = p.Angle };
                    _views[p.Id] = pv;
                }
                else
                {
                    if (p.Score > pv.Score)
                    {
                        pv.PulseUntil = now + 0.25;
                        SpawnSparkles(pv, p.X, p.Y);
                    }
                    pv.LastX = pv.X;  pv.LastY = pv.Y;  pv.X = p.X;  pv.Y = p.Y;
                    pv.LastAngle = pv.Angle;  pv.Angle = p.Angle;
                }
                pv.Score = p.Score;
                pv.BoostCharges = p.BoostCharges;
                pv.BoostActive  = p.BoostActive;
                pv.LastUpdate   = now;
            }

            // update orbs
            _orbsClient.Clear();
            foreach (var o in state.Orbs)
                _orbsClient.Add((o.X, o.Y));
        }

    }

    private void HandleGameOver(GameOverDto go)
    {
        _gameOver = true;
        _gameWon = go.WinnerId == _id;
        Console.WriteLine(_gameWon ? "You won!" : "You lost.");
    }

    private void HandleReset()
    {
        _gameOver = false;
        _gameWon = false;
        lock (_lock)
        {
            _views.Clear();
            _orbsClient.Clear();
        }

        Console.WriteLine("Game reset by server.");
    }

    // --- Public API ----------------------------------------------------------

    public void PollEvents() => _net.PollEvents();

    public void SendInput(float dx, float dy, bool boost)
    {
        if (_serverPeer == null) return;

        var msg  = new InputMessage(_id, new InputPayload(dx, dy, boost));
        var data = JsonSerializer.SerializeToUtf8Bytes(msg, WireContext.Default.InputMessage);
        _serverPeer.Send(data, DeliveryMethod.Unreliable);
    }
    
    private void SpawnSparkles(PlayerView pv, float cx, float cy)
    {
        var rnd = new Random();
        for (int i = 0; i < 20; i++)
        {
            var a = (float)(rnd.NextDouble() * Math.PI * 2);
            var s = 80f + (float)rnd.NextDouble() * 120f;
            pv.Particles.Add(new Particle { X = cx, Y = cy, Vx = MathF.Cos(a) * s, Vy = MathF.Sin(a) * s, Life = 0.3f + (float)rnd.NextDouble() * 0.2f });
        }
    }
    
    public Dictionary<string, (float x, float y, float angle, int score, int boostCharges, bool boostActive, double pulseUntil, List<Particle> particles)> 
        GetInterpolated()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, (float,float,float,int,int,bool,double,List<Particle>)>();
            double now = Raylib.GetTime();
            const double interpDelay = 0.10; // 100 ms buffer
            foreach (var kv in _views)
            {
                var v = kv.Value;
                double t = (now - v.LastUpdate) / interpDelay;
                t = Math.Clamp(t, 0.0, 1.0);

                float ix = (float)(v.LastX + (v.X - v.LastX) * t);
                float iy = (float)(v.LastY + (v.Y - v.LastY) * t);
                float ia = (float)(v.LastAngle + (NormalizeAngleDelta(v.Angle - v.LastAngle) * t));

                // update particles
                for (int i = v.Particles.Count - 1; i >= 0; i--)
                {
                    var p = v.Particles[i];
                    p.Life -= (float)(now - v.LastUpdate);
                    p.X += p.Vx * (float)(now - v.LastUpdate);
                    p.Y += p.Vy * (float)(now - v.LastUpdate);
                    v.Particles[i] = p;
                    if (p.Life <= 0) v.Particles.RemoveAt(i);
                }

                result[kv.Key] = (ix, iy, ia, v.Score, v.BoostCharges, v.BoostActive, v.PulseUntil, new List<Particle>(v.Particles));
            }
            return result;
        }
    }

    private static float NormalizeAngleDelta(float d)
    {
        while (d >  MathF.PI) d -= 2 * MathF.PI;
        while (d < -MathF.PI) d += 2 * MathF.PI;
        return d;
    }

}