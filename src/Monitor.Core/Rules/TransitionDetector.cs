using Monitor.Core.Model;

namespace Monitor.Core.Rules;

public static class TransitionDetector
{
    /// <summary>
    /// Compares two observations. Pipelines with no previous observation produce nothing, so the
    /// first poll after startup establishes a baseline instead of replaying old failures.
    /// </summary>
    public static IReadOnlyList<RunTransition> Detect(
        IReadOnlyDictionary<PipelineKey, PipelineStatus> previous,
        IEnumerable<PipelineStatus> current)
    {
        var transitions = new List<RunTransition>();

        foreach (var after in current)
        {
            if (!previous.TryGetValue(after.Key, out var before))
            {
                continue;
            }

            if (!before.IsBusy && after.IsBusy)
            {
                transitions.Add(new RunTransition(TransitionKind.Started, before, after));
            }

            var completed = after.LastCompletedRun;
            if (completed is not null && completed.RunId != before.LastCompletedRun?.RunId)
            {
                transitions.Add(new RunTransition(Classify(before.LastOutcome, completed.Outcome), before, after));
            }
        }

        return transitions;
    }

    private static TransitionKind Classify(RunOutcome previous, RunOutcome current) => current switch
    {
        RunOutcome.Failed when previous == RunOutcome.Failed => TransitionKind.StillFailing,
        RunOutcome.Failed => TransitionKind.Broke,
        RunOutcome.Succeeded when previous is RunOutcome.Failed or RunOutcome.PartiallySucceeded => TransitionKind.Fixed,
        _ => TransitionKind.Completed,
    };
}
