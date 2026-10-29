namespace Monitor.Core.Events;

/// <summary>
/// An optional push channel that tells the engine when to poll early, such as an Azure DevOps
/// service hook writing to an Azure Service Bus queue. Polling remains the source of truth;
/// a healthy feed only lets the engine poll less often while idle.
/// </summary>
public interface IEventFeed
{
    FeedHealth Health { get; }

    /// <summary>
    /// Streams refresh requests until <paramref name="cancellationToken"/> is canceled.
    /// Implementations handle their own reconnects and report them through <see cref="Health"/>.
    /// </summary>
    IAsyncEnumerable<RefreshRequest> ReadAllAsync(CancellationToken cancellationToken);
}
