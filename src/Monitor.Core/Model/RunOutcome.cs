namespace Monitor.Core.Model;

/// <summary>The result of a finished run. Maps directly onto CCTray's <c>lastBuildStatus</c>.</summary>
public enum RunOutcome
{
    Unknown,
    Succeeded,
    PartiallySucceeded,
    Failed,
    Canceled,
}
