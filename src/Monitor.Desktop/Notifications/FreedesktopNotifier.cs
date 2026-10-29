using System.Collections.Concurrent;
using System.Diagnostics;
using Tmds.DBus.Protocol;

namespace Monitor.Desktop.Notifications;

/// <summary>
/// Desktop notifications over the freedesktop.org Notifications D-Bus API, which Plasma, GNOME and
/// most other Linux desktops implement. Avalonia has no notification API of its own.
/// https://specifications.freedesktop.org/notification-spec/latest/
/// </summary>
internal sealed class FreedesktopNotifier : IDesktopNotifier
{
    private const string Service = "org.freedesktop.Notifications";
    private const string ObjectPath = "/org/freedesktop/Notifications";
    private const string Interface = "org.freedesktop.Notifications";
    private const string DefaultAction = "default";
    private const int MaxTrackedNotifications = 50;

    private readonly DBusConnection _connection;
    private readonly ConcurrentDictionary<uint, Uri> _urls = new();
    private IDisposable? _actionWatch;

    private FreedesktopNotifier(DBusConnection connection)
    {
        _connection = connection;
    }

    /// <summary>Connects to the session bus, or returns a no-op notifier when there is none.</summary>
    public static async Task<IDesktopNotifier> CreateAsync()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsFreeBSD() || DBusAddress.Session is not { } address)
        {
            return NullNotifier.Instance;
        }

        DBusConnection? connection = null;
        try
        {
            connection = new DBusConnection(address);
            await connection.ConnectAsync().ConfigureAwait(false);
            var notifier = new FreedesktopNotifier(connection);
            await notifier.WatchActionsAsync().ConfigureAwait(false);
            return notifier;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Desktop notifications unavailable: {ex.Message}");
            connection?.Dispose();
            return NullNotifier.Instance;
        }
    }

    public async Task NotifyAsync(string summary, string body, Uri? url)
    {
        try
        {
            var id = await _connection.CallMethodAsync(CreateNotifyMessage(summary, body, url is not null), ReadUInt32).ConfigureAwait(false);
            if (url is not null)
            {
                _urls[id] = url;
                PruneOldUrls();
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to show notification: {ex.Message}");
        }
    }

    public ValueTask DisposeAsync()
    {
        _actionWatch?.Dispose();

        _connection.Dispose();
        return ValueTask.CompletedTask;
    }

    private MessageBuffer CreateNotifyMessage(string summary, string body, bool hasAction)
    {
        // Notify(app_name s, replaces_id u, app_icon s, summary s, body s, actions as, hints a{sv}, expire_timeout i) -> u
        using var writer = _connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, ObjectPath, Interface, "Notify", "susssasa{sv}i");
        writer.WriteString("Monitor");
        writer.WriteUInt32(0);
        writer.WriteString(string.Empty);
        writer.WriteString(summary);
        writer.WriteString(body);
        writer.WriteArray(hasAction ? [DefaultAction, "Open run"] : Array.Empty<string>());
        var hints = writer.WriteDictionaryStart();
        writer.WriteDictionaryEnd(hints);
        writer.WriteInt32(-1);
        return writer.CreateMessage();
    }

    private async Task WatchActionsAsync()
    {
        _actionWatch = await _connection.WatchSignalAsync(
            Service, ObjectPath, Interface, "ActionInvoked",
            static (Message message, object? _) =>
            {
                var reader = message.GetBodyReader();
                return (Id: reader.ReadUInt32(), Action: reader.ReadString());
            },
            (Notification<(uint Id, string Action)> signal) =>
            {
                if (signal.HasValue && signal.Value.Action == DefaultAction && _urls.TryRemove(signal.Value.Id, out var url))
                {
                    BrowserLauncher.Open(url);
                }
            },
            ObserverFlags.None,
            false,
            null).ConfigureAwait(false);
    }

    // Notification IDs only grow, so dropping the smallest keeps the most recent ones clickable.
    // Not removed on NotificationClosed: some servers emit it before ActionInvoked.
    private void PruneOldUrls()
    {
        while (_urls.Count > MaxTrackedNotifications && _urls.Keys.DefaultIfEmpty().Min() is var oldest && _urls.TryRemove(oldest, out _))
        {
        }
    }

    private static uint ReadUInt32(Message message, object? state) => message.GetBodyReader().ReadUInt32();
}
