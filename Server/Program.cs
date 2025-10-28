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
var server = app.Services.GetRequiredService<GameServer>();

// start broadcast loop
var cts = new CancellationTokenSource();
_ = Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        try
        {
            await server.TickBroadcastAsync();
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
