using System.Net.WebSockets;
using Server;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin());
});
builder.Services.AddSingleton<GameServer>();
builder.Services.AddSingleton<NetworkChaos>();
var app = builder.Build();
app.UseCors();
app.MapGet("/", () => "Multiplayer Demo Server is running");
app.MapHub<NetworkHub>("/networkHub");

app.UseWebSockets();

var wsClients = new List<WebSocket>();
var wsLock = new object();

app.Map("/led", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        lock (wsLock) wsClients.Add(ws);

        Console.WriteLine("LED client connected");

        var buffer = new byte[1024];
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
                break;
        }

        lock (wsLock) wsClients.Remove(ws);
        Console.WriteLine("LED client disconnected");
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

var server = app.Services.GetRequiredService<GameServer>();

// start broadcast loop
var cts = new CancellationTokenSource();
_ = Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        try
        {
            await server.TickBroadcastAsync(wsClients, wsLock);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Broadcast error: " + ex);
        }
        await Task.Delay(25, cts.Token);
    }
});

app.Lifetime.ApplicationStopping.Register(() => cts.Cancel());

app.Run();
