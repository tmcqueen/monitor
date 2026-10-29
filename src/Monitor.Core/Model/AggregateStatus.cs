namespace Monitor.Core.Model;

public enum OverallHealth
{
    Unknown,
    Healthy,
    Warning,
    Broken,
}

/// <summary>The single status the tray icon shows.</summary>
public sealed record AggregateStatus(OverallHealth Health, bool IsBusy)
{
    public static AggregateStatus Unknown { get; } = new(OverallHealth.Unknown, false);
}
