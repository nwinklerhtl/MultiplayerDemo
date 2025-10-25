using System.Text.Json;
using Client.Model;
using LiteNetLib;
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

    public List<(float x, float y)> OrbsClient => _orbsClient;


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
        try
    {
        var json = System.Text.Encoding.UTF8.GetString(reader.GetRemainingBytes());
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.GetProperty("type").GetString() == "state")
        {
            var now = Raylib.GetTime();
            var players = doc.RootElement.GetProperty("players");
            var orbs = doc.RootElement.GetProperty("orbs");

            lock (_lock)
            {
                // update players
                foreach (var el in players.EnumerateArray())
                {
                    var pid = el.GetProperty("Id").GetString()!;
                    var x   = (float)el.GetProperty("X").GetDouble();
                    var y   = (float)el.GetProperty("Y").GetDouble();
                    var ang = (float)el.GetProperty("Angle").GetDouble();
                    var sc  = el.GetProperty("Score").GetInt32();
                    var bc  = el.GetProperty("BoostCharges").GetInt32();
                    var ba  = el.GetProperty("BoostActive").GetBoolean();

                    if (!_views.TryGetValue(pid, out var pv))
                    {
                        pv = new PlayerView { Id = pid, X = x, Y = y, LastX = x, LastY = y, Angle = ang, LastAngle = ang };
                        _views[pid] = pv;
                    }
                    else
                    {
                        // pulse effect if score increased
                        if (sc > pv.Score)
                        {
                            pv.PulseUntil = now + 0.25;
                            SpawnSparkles(pv, x, y);
                        }
                        pv.LastX = pv.X; pv.LastY = pv.Y; pv.X = x; pv.Y = y;
                        pv.LastAngle = pv.Angle; pv.Angle = ang;
                    }
                    pv.Score = sc;
                    pv.BoostCharges = bc;
                    pv.BoostActive = ba;
                    pv.LastUpdate = now;
                }

                // update orbs cache (simple list)
                _orbsClient.Clear();
                foreach (var el in orbs.EnumerateArray())
                {
                    _orbsClient.Add(( (float)el.GetProperty("X").GetDouble(),
                                      (float)el.GetProperty("Y").GetDouble() ));
                }
            }
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

    // --- Public API ----------------------------------------------------------

    public void PollEvents() => _net.PollEvents();

    public void SendInput(float dx, float dy, bool boost)
    {
        if (_serverPeer == null) return;

        var msg = new { type = "input", id = _id, input = new { dx, dy, boost } };
        var json = JsonSerializer.Serialize(msg);
        var data = System.Text.Encoding.UTF8.GetBytes(json);
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