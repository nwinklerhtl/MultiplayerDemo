using Microsoft.AspNetCore.SignalR;

namespace Server;

public class NetworkHub : Hub
{
    // clients (dashboard) will receive events: "PacketEvent" and "StateUpdate" (no code needed here)
    
    // optional: allow dashboard to call this if you want manual refresh etc.
    public Task Ping(string msg) => Clients.Caller.SendAsync("Pong", $"Server echo: {msg}");
}