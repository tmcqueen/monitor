using Monitor.Core.Events;
using Monitor.Core.Model;

namespace Monitor.Core.Engine;

/// <summary>Decides how long to wait between polls.</summary>
public sealed record PollingPolicy
{
    /// <summary>While anything is queued or running.</summary>
    public TimeSpan ActiveInterval { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>While everything is idle and no event feed is healthy.</summary>
    public TimeSpan IdleInterval { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>While everything is idle and the event feed is healthy; just a safety net.</summary>
    public TimeSpan ReconcileInterval { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>The ceiling for error backoff.</summary>
    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>The minimum gap between polls, so a burst of feed events causes one poll, not twenty.</summary>
    public TimeSpan MinSpacing { get; init; } = TimeSpan.FromSeconds(3);

    public TimeSpan NextDelay(AggregateStatus aggregate, FeedHealth feedHealth) =>
        aggregate.IsBusy ? ActiveInterval
        : feedHealth == FeedHealth.Healthy ? ReconcileInterval
        : IdleInterval;

    /// <summary>Exponential backoff after <paramref name="consecutiveFailures"/> failed polls.</summary>
    public TimeSpan Backoff(int consecutiveFailures)
    {
        var exponent = Math.Clamp(consecutiveFailures - 1, 0, 16);
        var delay = ActiveInterval * Math.Pow(2, exponent);
        return delay < MaxBackoff ? delay : MaxBackoff;
    }
}
