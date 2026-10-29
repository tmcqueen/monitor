using Monitor.Core.Model;

namespace Monitor.Core.Sources.AzureDevOps;

internal static class AdoBuildMapper
{
    /// <summary>
    /// Folds the recent builds of each requested definition into one <see cref="PipelineStatus"/>.
    /// <paramref name="builds"/> must be ordered newest first.
    /// </summary>
    public static IReadOnlyList<PipelineStatus> ToPipelineStatuses(
        string sourceId,
        IReadOnlyList<int> definitionIds,
        IReadOnlyList<AdoBuild> builds)
    {
        var byDefinition = builds
            .Where(b => b.Definition is not null)
            .GroupBy(b => b.Definition!.Id)
            .ToDictionary(g => g.Key, g => g.ToList());

        var statuses = new List<PipelineStatus>(definitionIds.Count);
        foreach (var definitionId in definitionIds)
        {
            var key = new PipelineKey(sourceId, definitionId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!byDefinition.TryGetValue(definitionId, out var recent) || recent.Count == 0)
            {
                statuses.Add(new PipelineStatus(key, $"Pipeline {definitionId}", RunActivity.Idle, null, null));
                continue;
            }

            var runs = recent.Select(ToRun).ToList();
            var activity = runs.Any(r => r.Activity == RunActivity.Running) ? RunActivity.Running
                : runs.Any(r => r.Activity == RunActivity.Queued) ? RunActivity.Queued
                : RunActivity.Idle;

            statuses.Add(new PipelineStatus(
                key,
                recent[0].Definition!.Name ?? $"Pipeline {definitionId}",
                activity,
                LatestRun: runs[0],
                LastCompletedRun: runs.FirstOrDefault(r => r.Activity == RunActivity.Idle)));
        }

        return statuses;
    }

    internal static PipelineRun ToRun(AdoBuild build) => new(
        build.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        build.BuildNumber ?? $"#{build.Id}",
        MapActivity(build.Status),
        MapOutcome(build.Result),
        build.RequestedFor?.DisplayName,
        ShortBranch(build.SourceBranch),
        build.QueueTime,
        build.FinishTime,
        Uri.TryCreate(build.Links?.Web?.Href, UriKind.Absolute, out var url) ? url : null);

    private static RunActivity MapActivity(string? status) => status?.ToLowerInvariant() switch
    {
        "completed" => RunActivity.Idle,
        "inprogress" or "cancelling" => RunActivity.Running,
        _ => RunActivity.Queued, // notStarted, postponed
    };

    private static RunOutcome MapOutcome(string? result) => result?.ToLowerInvariant() switch
    {
        "succeeded" => RunOutcome.Succeeded,
        "partiallysucceeded" => RunOutcome.PartiallySucceeded,
        "failed" => RunOutcome.Failed,
        "canceled" => RunOutcome.Canceled,
        _ => RunOutcome.Unknown,
    };

    private static string? ShortBranch(string? branch) =>
        branch is not null && branch.StartsWith("refs/heads/", StringComparison.Ordinal)
            ? branch["refs/heads/".Length..]
            : branch;
}
