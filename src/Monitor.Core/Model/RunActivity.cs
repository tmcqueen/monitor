namespace Monitor.Core.Model;

/// <summary>What a pipeline is doing right now. Maps directly onto CCTray's <c>activity</c>.</summary>
public enum RunActivity
{
    Idle,
    Queued,
    Running,
}
