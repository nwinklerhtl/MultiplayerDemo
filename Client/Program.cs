using System.Numerics;
using Client.Model;
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
            // normalize to avoid faster diagonal
            var len = MathF.Sqrt(dx * dx + dy * dy);
            if (len > 0.001f)
            {
                dx /= len;
                dy /= len;
            }

            bool boostPressed = Raylib.IsKeyPressed(KeyboardKey.Space);

            if ((now - lastInput).TotalMilliseconds >= 25) // 40 Hz to match server
            {
                client.SendInput(dx, dy, boostPressed);
                lastInput = now;
            }

            // simple local prediction
            px += dx * 5f;
            py += dy * 5f;

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(18, 22, 28, 255)); // dark bg

            DrawGridBackground(40, new Color(40, 46, 56, 255));
            DrawOrbs(client.OrbsClient);
            DrawPlayers(client.GetInterpolated(), id);
            DrawScoreboard(client, id);

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }

    private static void DrawGridBackground(int cell, Color line)
    {
        for (int x = 0; x <= 800; x += cell)
            Raylib.DrawLine(x, 0, x, 600, line);
        for (int y = 0; y <= 600; y += cell)
            Raylib.DrawLine(0, y, 800, y, line);
    }

    private static void DrawOrbs(List<(float x, float y)> orbs)
    {
        foreach (var (x, y) in orbs)
        {
            // glow: big faint, medium, core
            Raylib.DrawCircleGradient((int)x, (int)y, 22, new Color(20, 255, 200, 25), new Color(0, 0, 0, 0));
            Raylib.DrawCircleGradient((int)x, (int)y, 14, new Color(60, 255, 230, 60), new Color(0, 0, 0, 0));
            Raylib.DrawCircle((int)x, (int)y, 6, new Color(120, 255, 240, 255));
            Raylib.DrawCircleLines((int)x, (int)y, 10, new Color(120, 255, 240, 120));
        }
    }

    private static void DrawPlayers(
        Dictionary<string, (float x, float y, float ang, int score, int boostCharges, bool boostActive, double
            pulseUntil, List<Particle> parts)> views, string localId)
    {
        double now = Raylib.GetTime();
        foreach (var kv in views)
        {
            var pid = kv.Key;
            var v = kv.Value;
            bool isMe = pid == localId;

            // pulse glow on collect
            if (now < v.pulseUntil)
            {
                float t = (float)((v.pulseUntil - now) / 0.25);
                int alpha = (int)(120 * t);
                Raylib.DrawCircleGradient((int)v.x, (int)v.y, 28, new Color(255, 255, 120, alpha),
                    new Color(0, 0, 0, 0));
            }

            // particles
            foreach (var p in v.parts)
            {
                int a = (int)(255 * MathF.Max(0, p.Life / 0.5f));
                Raylib.DrawCircle((int)p.X, (int)p.Y, 2, new Color(255, 240, 120, a));
            }

            // thruster flame when moving or boosting
            bool moving = true; // or derive from input if you keep local input around
            if (moving)
            {
                float flameLen = v.boostActive ? 18f : 10f;
                DrawThrusterFlame(v.x, v.y, v.ang, flameLen);
            }

            // ship
            var body = isMe ? Color.SkyBlue : Color.Red;
            if (v.boostActive) body = new Color(255, 140, 0, 255); // orange when boosting
            DrawShipTriangle(v.x, v.y, v.ang, body);

            // name & score
            Raylib.DrawText(pid, (int)v.x + 14, (int)v.y - 22, 12, Color.LightGray);
        }
    }

    // private static void DrawShipTriangle(float x, float y, float ang, Color color)
    // {
    //     // simple isosceles triangle as spaceship
    //     float len = 18f;
    //     float half = 10f;
    //
    //     // forward
    //     var fx = x + MathF.Cos(ang) * len;
    //     var fy = y + MathF.Sin(ang) * len;
    //     // back-left
    //     var lx = x + MathF.Cos(ang + 2.6f) * half;
    //     var ly = y + MathF.Sin(ang + 2.6f) * half;
    //     // back-right
    //     var rx = x + MathF.Cos(ang - 2.6f) * half;
    //     var ry = y + MathF.Sin(ang - 2.6f) * half;
    //
    //     // does not work:
    //     Raylib.DrawTriangle(new System.Numerics.Vector2(lx, ly),
    //         new System.Numerics.Vector2(rx, ry),
    //         new System.Numerics.Vector2(fx, fy), color);
    //     // works instead:
    //     Raylib.DrawCircle((int)x, (int)y, 10, color);
    //     
    //     // outline (also works)
    //     Raylib.DrawTriangleLines(new System.Numerics.Vector2(lx, ly),
    //         new System.Numerics.Vector2(rx, ry),
    //         new System.Numerics.Vector2(fx, fy), Color.Black);
    // }
    
    private static void DrawShipTriangle(float x, float y, float ang, Color color)
    {
        // Define an upright isosceles triangle in LOCAL space (Y+ is forward here)
        Vector2 pTip   = new Vector2(0f, -16f);
        Vector2 pLeft  = new Vector2(-10f,  8f);
        Vector2 pRight = new Vector2( 10f,  8f);

        // rotate so 0 radians points right instead of up
        float c = MathF.Cos(ang + MathF.PI / 2);
        float s = MathF.Sin(ang + MathF.PI / 2);

        Vector2 vTip   = new Vector2(x + pTip.X * c - pTip.Y * s,   y + pTip.X * s + pTip.Y * c);
        Vector2 vLeft  = new Vector2(x + pLeft.X * c - pLeft.Y * s, y + pLeft.X * s + pLeft.Y * c);
        Vector2 vRight = new Vector2(x + pRight.X * c - pRight.Y * s,y + pRight.X * s + pRight.Y * c);

        // Filled triangle (ensure you pass System.Numerics.Vector2 to Raylib.DrawTriangle)
        Raylib.DrawTriangle(vLeft, vRight, vTip, color);

        // Outline on top
        Raylib.DrawTriangleLines(vLeft, vRight, vTip, Color.Black);
    }

    private static void DrawThrusterFlame(float x, float y, float ang, float length)
    {
        float backX = x - MathF.Cos(ang) * 10f;
        float backY = y - MathF.Sin(ang) * 10f;
        var tipX = backX - MathF.Cos(ang) * length;
        var tipY = backY - MathF.Sin(ang) * length;
        Raylib.DrawTriangle(
            new System.Numerics.Vector2(backX + MathF.Cos(ang + 1.2f) * 4f, backY + MathF.Sin(ang + 1.2f) * 4f),
            new System.Numerics.Vector2(backX + MathF.Cos(ang - 1.2f) * 4f, backY + MathF.Sin(ang - 1.2f) * 4f),
            new System.Numerics.Vector2(tipX, tipY),
            new Color(255, 200, 40, 200)
        );
    }

    private static void DrawScoreboard(SimpleClient cl, string me)
    {
        var views = cl.GetInterpolated();
        var list = new List<(string id, int score)>();
        foreach (var kv in views) list.Add((kv.Key, kv.Value.score));
        list.Sort((a, b) => b.score.CompareTo(a.score));

        int x = 10, y = 10, h = 20;
        Raylib.DrawRectangle(x - 6, y - 6, 180, h * (Math.Min(6, list.Count)) + 12, new Color(0, 0, 0, 120));
        Raylib.DrawText("Scores", x, y, 14, Color.Yellow);
        int row = 1;
        foreach (var (pid, sc) in list)
        {
            var col = pid == me ? Color.SkyBlue : Color.RayWhite;
            Raylib.DrawText($"{pid}: {sc}", x, y + row * h, 14, col);
            row++;
            if (row > 6) break;
        }
    }
}