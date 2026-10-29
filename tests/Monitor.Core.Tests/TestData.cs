using Monitor.Core.Model;

namespace Monitor.Core.Tests;

internal static class TestData
{
    public static readonly PipelineKey Key = new("src", "1");

    public static PipelineRun Run(string id, RunOutcome outcome, RunActivity activity = RunActivity.Idle) =>
        new(id, $"#{id}", activity, outcome, "Ada", "main", null, null, null);

    public static PipelineStatus Status(
        RunOutcome lastOutcome,
        string lastCompletedId = "1",
        RunActivity activity = RunActivity.Idle,
        PipelineKey? key = null)
    {
        var completed = lastOutcome == RunOutcome.Unknown ? null : Run(lastCompletedId, lastOutcome);
        return new PipelineStatus(key ?? Key, "CI", activity, completed, completed);
    }
}
