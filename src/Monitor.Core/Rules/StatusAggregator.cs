using Monitor.Core.Model;

namespace Monitor.Core.Rules;

public static class StatusAggregator
{
    /// <summary>
    /// Worst-of across all pipelines. Canceled runs are neutral: they neither break nor fix anything.
    /// </summary>
    public static AggregateStatus Compute(IEnumerable<PipelineStatus> pipelines)
    {
        var health = OverallHealth.Unknown;
        var busy = false;

        foreach (var pipeline in pipelines)
        {
            busy |= pipeline.IsBusy;
            var pipelineHealth = pipeline.LastOutcome switch
            {
                RunOutcome.Failed => OverallHealth.Broken,
                RunOutcome.PartiallySucceeded => OverallHealth.Warning,
                RunOutcome.Succeeded => OverallHealth.Healthy,
                _ => OverallHealth.Unknown,
            };

            if (pipelineHealth > health)
            {
                health = pipelineHealth;
            }
        }

        return new AggregateStatus(health, busy);
    }
}
