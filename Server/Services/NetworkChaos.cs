namespace Server.Services;

public sealed class NetworkChaos
{
    private readonly object _lock = new();
    private readonly Random _rng = new();
    private DateTime _untilUtc = DateTime.MinValue;

    public bool IsActive => DateTime.UtcNow < _untilUtc;
    public int LatencyMs { get; private set; } = 0;   // base delay
    public int JitterMs  { get; private set; } = 0;   // +/- jitter
    public double Loss   { get; private set; } = 0.0; // 0..1 probability

    public void Trigger(TimeSpan duration, int latencyMs = 120, int jitterMs = 40, double loss = 0.30)
    {
        lock (_lock)
        {
            LatencyMs = Math.Max(0, latencyMs);
            JitterMs = Math.Max(0, jitterMs);
            Loss = Math.Clamp(loss, 0.0, 1.0);
            _untilUtc = DateTime.UtcNow.Add(duration);
        }
    }

    public void Clear()
    {
        lock (_lock) { _untilUtc = DateTime.MinValue; }
    }

    public bool ShouldDrop() => IsActive && _rng.NextDouble() < Loss;

    public int DelayMs()
    {
        if (!IsActive) return 0;
        var jitter = _rng.Next(-JitterMs, JitterMs + 1);
        return Math.Max(0, LatencyMs + jitter);
    }

    public ChaosDto ToDto()
    {
        var remaining = IsActive ? (_untilUtc - DateTime.UtcNow) : TimeSpan.Zero;
        return new ChaosDto(IsActive, Math.Max(0, (int)remaining.TotalMilliseconds), LatencyMs, JitterMs, Loss);
    }
}

public sealed record ChaosDto(bool Active, int RemainingMs, int LatencyMs, int JitterMs, double Loss);
