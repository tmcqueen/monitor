namespace Monitor.Desktop.Notifications;

internal interface IDesktopNotifier : IAsyncDisposable
{
    /// <summary>Shows a desktop notification. When <paramref name="url"/> is set, clicking it opens the URL.</summary>
    Task NotifyAsync(string summary, string body, Uri? url);
}

internal sealed class NullNotifier : IDesktopNotifier
{
    public static NullNotifier Instance { get; } = new();

    public Task NotifyAsync(string summary, string body, Uri? url) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
