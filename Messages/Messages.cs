using System.Text.Json;
using System.Text.Json.Serialization;

namespace Messages;

// -------- Client -> Server --------

public sealed record InputMessage(
    [property: JsonPropertyName("id")]    string Id,
    [property: JsonPropertyName("input")] InputPayload Input
);

public sealed record InputPayload(
    [property: JsonPropertyName("dx")] float Dx,
    [property: JsonPropertyName("dy")] float Dy,
    [property: JsonPropertyName("boost")] bool Boost
);

// -------- Server -> Client --------
public enum MessageType
{
    State,
    GameOver,
    Reset
}

public sealed record Envelope<T>(
    [property: JsonPropertyName("type")] MessageType Type,
    [property: JsonPropertyName("payload")]
    T Payload
);

public sealed record StateMessage(
    [property: JsonPropertyName("players")]
    IReadOnlyList<PlayerDto> Players,
    [property: JsonPropertyName("orbs")] IReadOnlyList<OrbDto> Orbs
)
{
    public string StateToString() => $"{string.Join(" | ", Players.Select(p => $"{p.Id}:({p.X},{p.Y})"))} + {string.Join(" | ", Orbs.Select(o => $"O({o.X},{o.Y})"))}";
};

public sealed record PlayerDto(
    [property: JsonPropertyName("id")]           string Id,
    [property: JsonPropertyName("x")]            float  X,
    [property: JsonPropertyName("y")]            float  Y,
    [property: JsonPropertyName("angle")]        float  Angle,
    [property: JsonPropertyName("score")]        int    Score,
    [property: JsonPropertyName("boostCharges")] int    BoostCharges,
    [property: JsonPropertyName("boostActive")]  bool   BoostActive
);

public sealed record OrbDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("x")]  float  X,
    [property: JsonPropertyName("y")]  float  Y
);

public sealed record GameOverDto(
    [property: JsonPropertyName("winnerId")]
    string WinnerId
);

public sealed record ResetDto; 

// -------- Server -> Dashboard --------

public sealed record SignalRStateMessage(
    [property: JsonPropertyName("time")] DateTime Time,
    [property: JsonPropertyName("payload")] StateMessage Payload
);

public sealed record SignalRPacketMessage(
    [property: JsonPropertyName("src")] string Src,
    [property: JsonPropertyName("payload")] InputMessage Payload,
    [property: JsonPropertyName("time")] DateTime Time
);

public sealed record ChaosDto(bool Active, int RemainingMs, int LatencyMs, int JitterMs, double Loss);

// (Optional) central JSON options you can reuse on both sides
public static class JsonWire
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true, // tolerate "type" vs "Type" etc.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
        // If you introduce enums later:
        // Converters = { new JsonStringEnumConverter() }
    };
}