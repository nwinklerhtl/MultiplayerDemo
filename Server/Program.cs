using Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<GameServer>();
var app = builder.Build();
app.MapGet("/", () => "Multiplayer Demo Server is running");
app.MapHub<NetworkHub>("/networkHub");
var server = app.Services.GetRequiredService<GameServer>();

// start broadcast loop
var cts = new CancellationTokenSource();
_ = Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        try
        {
            server.TickBroadcast();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Broadcast error: " + ex);
        }
        await Task.Delay(25);
    }
});

app.Lifetime.ApplicationStopping.Register(() => cts.Cancel());

app.Run();
