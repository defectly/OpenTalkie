namespace OpenTalkie.Application.Streams;

public enum StreamSessionPhase
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Faulted
}

public readonly record struct StreamSessionStatus(StreamSessionPhase Phase, string? ErrorMessage = null)
{
    public bool IsActive => Phase is StreamSessionPhase.Starting or StreamSessionPhase.Running or StreamSessionPhase.Stopping;

    public static StreamSessionStatus Stopped() => new(StreamSessionPhase.Stopped);
    public static StreamSessionStatus Starting() => new(StreamSessionPhase.Starting);
    public static StreamSessionStatus Running() => new(StreamSessionPhase.Running);
    public static StreamSessionStatus Stopping() => new(StreamSessionPhase.Stopping);
    public static StreamSessionStatus Faulted(string? errorMessage) => new(StreamSessionPhase.Faulted, errorMessage);
}
