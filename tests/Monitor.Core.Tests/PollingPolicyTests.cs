using Monitor.Core.Engine;
using Monitor.Core.Events;
using Monitor.Core.Model;

namespace Monitor.Core.Tests;

public class PollingPolicyTests
{
    private readonly PollingPolicy _policy = new();

    [Fact]
    public void BusyPollsFastEvenWithAHealthyFeed() =>
        Assert.Equal(_policy.ActiveInterval, _policy.NextDelay(new(OverallHealth.Healthy, true), FeedHealth.Healthy));

    [Fact]
    public void IdleWithoutFeedUsesIdleInterval() =>
        Assert.Equal(_policy.IdleInterval, _policy.NextDelay(new(OverallHealth.Healthy, false), FeedHealth.Unavailable));

    [Fact]
    public void IdleWithHealthyFeedOnlyReconciles() =>
        Assert.Equal(_policy.ReconcileInterval, _policy.NextDelay(new(OverallHealth.Healthy, false), FeedHealth.Healthy));

    [Fact]
    public void BackoffDoublesUpToTheCap()
    {
        Assert.Equal(_policy.ActiveInterval, _policy.Backoff(1));
        Assert.Equal(_policy.ActiveInterval * 2, _policy.Backoff(2));
        Assert.Equal(_policy.ActiveInterval * 4, _policy.Backoff(3));
        Assert.Equal(_policy.MaxBackoff, _policy.Backoff(50));
    }
}
