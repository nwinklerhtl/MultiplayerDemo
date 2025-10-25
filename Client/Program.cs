using Raylib_cs;

namespace Client;

public static class Program
{
    public static void Main()
    {
        Console.Write("Enter player id: ");
        var id = Console.ReadLine() ?? Guid.NewGuid().ToString()[..8];

        var client = new SimpleClient(id);

        Raylib.InitWindow(800, 600, $"Client {id}");
        Raylib.SetTargetFPS(60);

        float px = 400f, py = 300f;
        var lastInput = DateTime.UtcNow;

        while (!Raylib.WindowShouldClose())
        {
            client.PollEvents();

            // input
            float dx = 0f, dy = 0f;
            if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) dy -= 1f;
            if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) dy += 1f;
            if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) dx -= 1f;
            if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) dx += 1f;

            var now = DateTime.UtcNow;
            if ((now - lastInput).TotalMilliseconds >= 25)
            {
                client.SendInput(dx, dy);
                lastInput = now;
            }

            // simple local prediction
            px += dx * 5f;
            py += dy * 5f;

            var players = client.GetInterpolatedPlayers();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.RayWhite);

            foreach (var kv in players)
            {
                var pid = kv.Key;
                var (x,y) = kv.Value;
                if (pid == id)
                {
                    Raylib.DrawCircle((int)x, (int)y, 12, Color.Blue);
                }
                else
                {
                    Raylib.DrawCircle((int)x, (int)y, 10, Color.Red);
                }
                Raylib.DrawText(pid, (int)x + 14, (int)y - 6, 10, Color.DarkGray);
            }

            Raylib.DrawText($"You: {id}", 10, 10, 14, Color.DarkGray);
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}