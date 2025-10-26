using System.Text.Json.Serialization;

namespace Messages;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(InputMessage))]
[JsonSerializable(typeof(StateMessage))]
[JsonSerializable(typeof(SignalRStateMessage))]
public partial class WireContext : JsonSerializerContext { }