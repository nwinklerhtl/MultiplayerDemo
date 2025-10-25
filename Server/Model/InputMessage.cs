namespace Server.Model;

public record InputMessage(string Type, string Id, InputPayload Input);
public record InputPayload(float Dx, float Dy, bool Boost);