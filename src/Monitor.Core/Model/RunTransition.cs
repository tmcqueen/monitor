namespace Monitor.Core.Model;

public enum TransitionKind
{
    /// <summary>A pipeline went from idle to queued or running.</summary>
    Started,

    /// <summary>A run failed after the previous completed run did not.</summary>
    Broke,

    /// <summary>A run failed and the previous completed run had also failed.</summary>
    StillFailing,

    /// <summary>A run succeeded after the previous completed run failed or partially succeeded.</summary>
    Fixed,

    /// <summary>Any other completion (success after success, cancellation, and so on).</summary>
    Completed,
}

public sealed record RunTransition(TransitionKind Kind, PipelineStatus Before, PipelineStatus After);
