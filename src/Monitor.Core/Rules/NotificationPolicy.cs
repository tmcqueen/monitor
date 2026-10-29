using Monitor.Core.Model;

namespace Monitor.Core.Rules;

public static class NotificationPolicy
{
    /// <summary>Only state changes worth interrupting someone for: it broke, or it got fixed.</summary>
    public static bool ShouldNotify(RunTransition transition) =>
        transition.Kind is TransitionKind.Broke or TransitionKind.Fixed;
}
