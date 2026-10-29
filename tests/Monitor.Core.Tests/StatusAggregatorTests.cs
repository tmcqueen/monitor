using Monitor.Core.Model;
using Monitor.Core.Rules;
using static Monitor.Core.Tests.TestData;

namespace Monitor.Core.Tests;

public class StatusAggregatorTests
{
    [Fact]
    public void NoPipelinesIsUnknown() =>
        Assert.Equal(AggregateStatus.Unknown, StatusAggregator.Compute([]));

    [Fact]
    public void WorstOutcomeWins()
    {
        var result = StatusAggregator.Compute(
        [
            Status(RunOutcome.Succeeded, key: new("s", "1")),
            Status(RunOutcome.Failed, key: new("s", "2")),
            Status(RunOutcome.PartiallySucceeded, key: new("s", "3")),
        ]);

        Assert.Equal(OverallHealth.Broken, result.Health);
    }

    [Fact]
    public void CanceledRunsAreNeutral()
    {
        var result = StatusAggregator.Compute(
        [
            Status(RunOutcome.Succeeded, key: new("s", "1")),
            Status(RunOutcome.Canceled, key: new("s", "2")),
        ]);

        Assert.Equal(OverallHealth.Healthy, result.Health);
    }

    [Fact]
    public void BusyWhenAnyPipelineIsActive()
    {
        var result = StatusAggregator.Compute(
        [
            Status(RunOutcome.Succeeded, key: new("s", "1")),
            Status(RunOutcome.Succeeded, activity: RunActivity.Queued, key: new("s", "2")),
        ]);

        Assert.True(result.IsBusy);
        Assert.Equal(OverallHealth.Healthy, result.Health);
    }
}
