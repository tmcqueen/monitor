namespace Monitor.Core.Model;

/// <summary>
/// The state of one monitored pipeline. Health comes from the last <em>completed</em> run, while
/// <see cref="Activity"/> says whether something is in flight. This is the same split CCTray uses
/// (<c>lastBuildStatus</c> plus <c>activity</c>), so a running pipeline still shows its last known health.
/// </summary>
public sealed record PipelineStatus(
    PipelineKey Key,
    string Name,
    RunActivity Activity,
    PipelineRun? LatestRun,
    PipelineRun? LastCompletedRun)
{
    public RunOutcome LastOutcome => LastCompletedRun?.Outcome ?? RunOutcome.Unknown;

    public bool IsBusy => Activity != RunActivity.Idle;
}
