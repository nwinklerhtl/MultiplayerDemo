using Microsoft.AspNetCore.SignalR;
using Server.Services;

namespace Server;

public class NetworkHub(NetworkChaos chaos) : Hub
{
    private readonly NetworkChaos _chaos = chaos;
    // clients (dashboard) will receive events: "PacketEvent" and "StateUpdate" (no code needed here)
    
    // optional: allow dashboard to call this if you want manual refresh etc.
    public Task Ping(string msg) => Clients.Caller.SendAsync("Pong", $"Server echo: {msg}");
    
    // Dashboard calls this to start "panic mode"
    public async Task Panic(int durationSeconds = 10, int latencyMs = 120, int jitterMs = 40, double loss = 0.30)
    {
        _chaos.Trigger(TimeSpan.FromSeconds(durationSeconds), latencyMs, jitterMs, loss);
        await Clients.All.SendAsync("ChaosStatus", _chaos.ToDto());
    }

    // Dashboard can query current status
    public Task<ChaosDto> GetChaosStatus() => Task.FromResult(_chaos.ToDto());

    // Optional: manual clear
    public async Task ClearPanic()
    {
        _chaos.Clear();
        await Clients.All.SendAsync("ChaosStatus", _chaos.ToDto());
    }
}