using Monitor.Core.Events;

namespace Monitor.Core.Model;

/// <summary>Everything the UI needs to render, published after every poll.</summary>
public sealed record MonitorSnapshot(
    IReadOnlyList<PipelineStatus> Pipelines,
    AggregateStatus Aggregate,
    DateTimeOffset? LastUpdated,
    string? LastError,
    FeedHealth FeedHealth)
{
    public static MonitorSnapshot Empty { get; } =
        new([], AggregateStatus.Unknown, null, null, FeedHealth.Unavailable);
}
