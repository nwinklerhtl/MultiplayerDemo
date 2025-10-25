namespace Server.Model;

public class SimPlayer
{
    public string Id = "";
    public float X, Y;
    public float Angle;            // radians
    public float Speed = 160f;     // px/s base speed
    public int Score;
    public int BoostCharges;       // +1 per orb
    public bool BoostActive;
    public double BoostUntil;      // absolute time (seconds since start) when boost ends
    public float InputDx, InputDy; // last input
    public bool ConsumeBoost;      // set true by incoming input edge; consumed at tick
}