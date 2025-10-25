using System.Text.Json;
using System.Text.Json.Serialization;

namespace Messages;

// -------- Client -> Server --------

public sealed record InputMessage(
    [property: JsonPropertyName("id")]    string Id,
    [property: JsonPropertyName("input")] InputPayload Input
);

public sealed record InputPayload(
    [property: JsonPropertyName("dx")]    float Dx,
    [property: JsonPropertyName("dy")]    float Dy,
    [property: JsonPropertyName("boost")] bool  Boost
);

// -------- Server -> Client --------

public sealed record StateMessage(
    [property: JsonPropertyName("players")] IReadOnlyList<PlayerDto> Players,
    [property: JsonPropertyName("orbs")]    IReadOnlyList<OrbDto>    Orbs
);

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