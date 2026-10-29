using Monitor.Core.Engine;
using Monitor.Core.Events;
using Monitor.Core.Model;
using Monitor.Core.Sources;
using static Monitor.Core.Tests.TestData;

namespace Monitor.Core.Tests;

public class MonitorEngineTests
{
    private readonly PollingPolicy _policy = new();

    [Fact]
    public async Task FirstPollIsABaselineThenTransitionsAreRaised()
    {
        var source = new FakeSource { Next = () => [Status(RunOutcome.Succeeded, "1")] };
        var engine = new MonitorEngine([source], NullEventFeed.Instance, _policy, TimeProvider.System);
        var transitions = new List<RunTransition>();
        engine.TransitionDetected += (_, t) => transitions.Add(t);

        await engine.PollOnceAsync(CancellationToken.None);
        Assert.Empty(transitions);

        source.Next = () => [Status(RunOutcome.Failed, "2")];
        await engine.PollOnceAsync(CancellationToken.None);

        Assert.Equal(TransitionKind.Broke, Assert.Single(transitions).Kind);
        Assert.Equal(OverallHealth.Broken, engine.Current.Aggregate.Health);
    }

    [Fact]
    public async Task AFailingSourceKeepsItsLastKnownStateAndBacksOff()
    {
        var source = new FakeSource { Next = () => [Status(RunOutcome.Succeeded, "1")] };
        var engine = new MonitorEngine([source], NullEventFeed.Instance, _policy, TimeProvider.System);
        await engine.PollOnceAsync(CancellationToken.None);

        source.Next = () => throw new HttpRequestException("network down");
        var first = await engine.PollOnceAsync(CancellationToken.None);
        var second = await engine.PollOnceAsync(CancellationToken.None);

        Assert.Single(engine.Current.Pipelines);
        Assert.Equal(OverallHealth.Healthy, engine.Current.Aggregate.Health);
        Assert.Contains("network down", engine.Current.LastError);
        Assert.Equal(_policy.Backoff(1), first);
        Assert.Equal(_policy.Backoff(2), second);
    }

    [Fact]
    public async Task ThrottlingDelaysTheNextPoll()
    {
        var source = new FakeSource { Next = () => throw new SourceThrottledException(TimeSpan.FromMinutes(7), "slow down") };
        var engine = new MonitorEngine([source], NullEventFeed.Instance, _policy, TimeProvider.System);

        Assert.Equal(TimeSpan.FromMinutes(7), await engine.PollOnceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FeedEventsWakeTheEngineEarly()
    {
        var source = new FakeSource { Next = () => [Status(RunOutcome.Succeeded)] };
        var feed = new ManualFeed();
        var policy = _policy with { IdleInterval = TimeSpan.FromHours(1), ReconcileInterval = TimeSpan.FromHours(1), MinSpacing = TimeSpan.Zero };
        var engine = new MonitorEngine([source], feed, policy, TimeProvider.System);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var run = engine.RunAsync(cts.Token);
        await source.WaitForPollsAsync(1, cts.Token);

        await feed.PublishAsync(new RefreshRequest("src", "1"));
        await source.WaitForPollsAsync(2, cts.Token);

        await cts.CancelAsync();
        await run;
        Assert.True(source.Polls >= 2);
    }

    private sealed class FakeSource : IRunSource
    {
        private int _polls;

        public required Func<IReadOnlyList<PipelineStatus>> Next { get; set; }

        public string Id => "src";

        public string DisplayName => "Fake";

        public int Polls => Volatile.Read(ref _polls);

        public Task<IReadOnlyList<PipelineStatus>> GetPipelineStatusesAsync(CancellationToken cancellationToken)
        {
            try
            {
                return Task.FromResult(Next());
            }
            finally
            {
                Interlocked.Increment(ref _polls);
            }
        }

        public async Task WaitForPollsAsync(int count, CancellationToken cancellationToken)
        {
            while (Polls < count)
            {
                await Task.Delay(10, cancellationToken);
            }
        }
    }

    private sealed class ManualFeed : IEventFeed
    {
        private readonly System.Threading.Channels.Channel<RefreshRequest> _channel =
            System.Threading.Channels.Channel.CreateUnbounded<RefreshRequest>();

        public FeedHealth Health => FeedHealth.Healthy;

        public ValueTask PublishAsync(RefreshRequest request) => _channel.Writer.WriteAsync(request);

        public IAsyncEnumerable<RefreshRequest> ReadAllAsync(CancellationToken cancellationToken) =>
            _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
