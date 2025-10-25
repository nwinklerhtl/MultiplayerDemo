namespace Client.Model;

public class PlayerView
{
    public string Id = "";
    public float X, Y;         // latest server pos
    public float LastX, LastY; // previous server pos
    public double LastUpdate;  // when latest arrived (Raylib.GetTime())
    public float Angle, LastAngle;
    public int Score;
    public int BoostCharges;
    public bool BoostActive;

    // visual effects
    public double PulseUntil;       // time until which to draw a pulse glow (on collect)
    public List<Particle> Particles = new(); // sparkle particles on collect/boost
}