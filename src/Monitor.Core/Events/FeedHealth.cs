namespace Monitor.Core.Events;

public enum FeedHealth
{
    /// <summary>No feed is configured, or it cannot connect. Polling runs at full speed.</summary>
    Unavailable,

    /// <summary>The feed is connecting or reconnecting. Polling runs at full speed.</summary>
    Degraded,

    /// <summary>The feed is connected. Background polling slows to a reconciliation pass.</summary>
    Healthy,
}
