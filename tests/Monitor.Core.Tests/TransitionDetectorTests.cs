using Monitor.Core.Model;
using Monitor.Core.Rules;
using static Monitor.Core.Tests.TestData;

namespace Monitor.Core.Tests;

public class TransitionDetectorTests
{
    private static IReadOnlyList<RunTransition> Detect(PipelineStatus? before, PipelineStatus after)
    {
        var previous = before is null
            ? new Dictionary<PipelineKey, PipelineStatus>()
            : new Dictionary<PipelineKey, PipelineStatus> { [before.Key] = before };
        return TransitionDetector.Detect(previous, [after]);
    }

    [Fact]
    public void FirstObservationIsABaseline() =>
        Assert.Empty(Detect(null, Status(RunOutcome.Failed)));

    [Fact]
    public void UnchangedStateProducesNothing() =>
        Assert.Empty(Detect(Status(RunOutcome.Failed, "7"), Status(RunOutcome.Failed, "7")));

    [Theory]
    [InlineData(RunOutcome.Succeeded, RunOutcome.Failed, TransitionKind.Broke)]
    [InlineData(RunOutcome.Unknown, RunOutcome.Failed, TransitionKind.Broke)]
    [InlineData(RunOutcome.Failed, RunOutcome.Failed, TransitionKind.StillFailing)]
    [InlineData(RunOutcome.Failed, RunOutcome.Succeeded, TransitionKind.Fixed)]
    [InlineData(RunOutcome.PartiallySucceeded, RunOutcome.Succeeded, TransitionKind.Fixed)]
    [InlineData(RunOutcome.Succeeded, RunOutcome.Succeeded, TransitionKind.Completed)]
    [InlineData(RunOutcome.Failed, RunOutcome.Canceled, TransitionKind.Completed)]
    public void ClassifiesNewCompletedRuns(RunOutcome before, RunOutcome after, TransitionKind expected)
    {
        var transition = Assert.Single(Detect(Status(before, "1"), Status(after, "2")));
        Assert.Equal(expected, transition.Kind);
    }

    [Fact]
    public void DetectsStart()
    {
        var transition = Assert.Single(Detect(
            Status(RunOutcome.Succeeded, "1"),
            Status(RunOutcome.Succeeded, "1", RunActivity.Running)));

        Assert.Equal(TransitionKind.Started, transition.Kind);
    }

    [Fact]
    public void OnlyBrokeAndFixedNotify()
    {
        var notifying = Enum.GetValues<TransitionKind>()
            .Where(k => NotificationPolicy.ShouldNotify(new RunTransition(k, Status(RunOutcome.Unknown), Status(RunOutcome.Unknown))));

        Assert.Equal([TransitionKind.Broke, TransitionKind.Fixed], notifying);
    }
}
