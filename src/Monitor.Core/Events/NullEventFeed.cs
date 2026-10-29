namespace Monitor.Core.Events;

/// <summary>The feed used when none is configured. Never produces events.</summary>
public sealed class NullEventFeed : IEventFeed
{
    public static NullEventFeed Instance { get; } = new();

    private NullEventFeed()
    {
    }

    public FeedHealth Health => FeedHealth.Unavailable;

    public IAsyncEnumerable<RefreshRequest> ReadAllAsync(CancellationToken cancellationToken) =>
        AsyncEnumerable.Empty<RefreshRequest>();
}
