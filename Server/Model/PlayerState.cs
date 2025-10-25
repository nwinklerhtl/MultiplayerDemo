namespace Server.Model;

public record PlayerState(string Id, float X, float Y, float Angle, int Score, int BoostCharges, bool BoostActive);