namespace Monitor.Core.Model;

/// <summary>A single run (build) of a pipeline.</summary>
public sealed record PipelineRun(
    string RunId,
    string RunName,
    RunActivity Activity,
    RunOutcome Outcome,
    string? RequestedBy,
    string? Branch,
    DateTimeOffset? QueuedAt,
    DateTimeOffset? FinishedAt,
    Uri? WebUrl);
