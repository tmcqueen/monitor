using System.Threading.Channels;
using Monitor.Core.Events;
using Monitor.Core.Model;
using Monitor.Core.Rules;
using Monitor.Core.Sources;

namespace Monitor.Core.Engine;

/// <summary>
/// Polls every source on an adaptive schedule, detects transitions, and publishes snapshots.
/// An <see cref="IEventFeed"/> can wake it early; it never replaces polling.
/// Events are raised on the engine's own background thread.
/// </summary>
public sealed class MonitorEngine
{
    private readonly IReadOnlyList<IRunSource> _sources;
    private readonly IEventFeed _feed;
    private readonly PollingPolicy _policy;
    private readonly TimeProvider _time;
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Dictionary<PipelineKey, PipelineStatus> _known = [];
    private int _consecutiveFailures;

    public MonitorEngine(IReadOnlyList<IRunSource> sources, IEventFeed feed, PollingPolicy policy, TimeProvider time)
    {
        _sources = sources;
        _feed = feed;
        _policy = policy;
        _time = time;
    }

    public event EventHandler<MonitorSnapshot>? SnapshotUpdated;

    public event EventHandler<RunTransition>? TransitionDetected;

    public MonitorSnapshot Current { get; private set; } = MonitorSnapshot.Empty;

    /// <summary>Asks the engine to poll as soon as <see cref="PollingPolicy.MinSpacing"/> allows.</summary>
    public void RequestRefresh() => _wake.Writer.TryWrite(true);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var feedPump = PumpFeedAsync(cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var started = _time.GetUtcNow();
                var delay = await PollOnceAsync(cancellationToken).ConfigureAwait(false);
                await WaitAsync(started, delay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await feedPump.ConfigureAwait(false);
        }
    }

    /// <summary>Polls every source once and returns how long to wait before the next poll.</summary>
    public async Task<TimeSpan> PollOnceAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var throttleDelay = TimeSpan.Zero;
        var current = new List<PipelineStatus>();

        foreach (var source in _sources)
        {
            try
            {
                current.AddRange(await source.GetPipelineStatusesAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (ex is SourceThrottledException throttled)
                {
                    throttleDelay = throttled.RetryAfter > throttleDelay ? throttled.RetryAfter : throttleDelay;
                }

                errors.Add($"{source.DisplayName}: {ex.Message}");

                // Keep showing what we last knew about a failing source rather than blanking it.
                current.AddRange(_known.Values.Where(p => p.Key.SourceId == source.Id));
            }
        }

        var transitions = TransitionDetector.Detect(_known, current);
        _known.Clear();
        foreach (var pipeline in current)
        {
            _known[pipeline.Key] = pipeline;
        }

        var aggregate = StatusAggregator.Compute(current);
        var failedEverything = errors.Count > 0 && errors.Count == _sources.Count;
        _consecutiveFailures = failedEverything ? _consecutiveFailures + 1 : 0;

        Current = new MonitorSnapshot(
            current.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            aggregate,
            failedEverything ? Current.LastUpdated : _time.GetUtcNow(),
            errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null,
            _feed.Health);

        foreach (var transition in transitions)
        {
            TransitionDetected?.Invoke(this, transition);
        }

        SnapshotUpdated?.Invoke(this, Current);

        var next = failedEverything ? _policy.Backoff(_consecutiveFailures) : _policy.NextDelay(aggregate, _feed.Health);
        return throttleDelay > next ? throttleDelay : next;
    }

    private async Task WaitAsync(DateTimeOffset pollStarted, TimeSpan delay, CancellationToken cancellationToken)
    {
        using (var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var timer = Task.Delay(delay, _time, waitCts.Token);
            var wake = _wake.Reader.WaitToReadAsync(waitCts.Token).AsTask();
            await Task.WhenAny(timer, wake).ConfigureAwait(false);
            await waitCts.CancelAsync().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        while (_wake.Reader.TryRead(out _))
        {
        }

        // Coalesce bursts: never poll more often than MinSpacing, even when woken early.
        var sinceLastPoll = _time.GetUtcNow() - pollStarted;
        if (sinceLastPoll < _policy.MinSpacing)
        {
            await Task.Delay(_policy.MinSpacing - sinceLastPoll, _time, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PumpFeedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in _feed.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                RequestRefresh();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            // A broken feed must never stop polling. The feed reports its own health.
        }
    }
}
