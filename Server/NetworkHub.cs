using Microsoft.AspNetCore.SignalR;

namespace Server;

public class NetworkHub : Hub
{
    // clients (dashboard) will receive events: "PacketEvent" and "StateUpdate"
}