using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Monitor.Core.Engine;
using Monitor.Core.Events;
using Monitor.Core.Model;
using Monitor.Core.Rules;
using Monitor.Core.Sources.AzureDevOps;
using Monitor.Desktop.Notifications;
using Monitor.Desktop.Tray;

namespace Monitor.Desktop;

public sealed partial class App : Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private TrayIcon? _tray;
    private StatusWindow? _window;
    private MonitorEngine? _engine;
    private Task? _engineTask;
    private HttpClient? _http;
    private IDesktopNotifier _notifier = NullNotifier.Instance;
    private string? _configError;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => Stop();
        }

        _tray = new TrayIcon { IsVisible = true };
        _tray.Clicked += (_, _) => ShowStatusWindow();
        TrayIcon.SetIcons(this, [_tray]);

        if (EnvironmentConfig.TryLoad(out var options, out var error))
        {
            _ = StartAsync(options!);
        }
        else
        {
            _configError = error;
        }

        Render(MonitorSnapshot.Empty);
        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartAsync(AdoOptions options)
    {
        _notifier = await FreedesktopNotifier.CreateAsync();
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // Phase 1.5: swap NullEventFeed for a Service Bus feed fed by ADO service hooks.
        _engine = new MonitorEngine([new AdoRunSource(options, _http)], NullEventFeed.Instance, new PollingPolicy(), TimeProvider.System);
        _engine.SnapshotUpdated += (_, snapshot) => Dispatcher.UIThread.Post(() => Render(snapshot));
        _engine.TransitionDetected += (_, transition) =>
        {
            if (NotificationPolicy.ShouldNotify(transition))
            {
                _ = _notifier.NotifyAsync(Summarize(transition), Describe(transition), transition.After.LastCompletedRun?.WebUrl);
            }
        };
        _engineTask = Task.Run(() => _engine.RunAsync(_shutdown.Token));
    }

    private void Stop()
    {
        _shutdown.Cancel();
        try
        {
            _engineTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Shutting down anyway.
        }

        _notifier.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(1));
        _http?.Dispose();
    }

    private void Render(MonitorSnapshot snapshot)
    {
        if (_tray is null)
        {
            return;
        }

        _tray.Icon = TrayIconRenderer.Render(snapshot.Aggregate);
        _tray.ToolTipText = Tooltip(snapshot);
        _tray.Menu = BuildMenu(snapshot);
        _window?.Update(snapshot, _configError);
    }

    private string Tooltip(MonitorSnapshot snapshot)
    {
        if (_configError is not null)
        {
            return $"Monitor: {_configError}";
        }

        if (snapshot.Pipelines.Count == 0)
        {
            return snapshot.LastError is null ? "Monitor: starting…" : $"Monitor: {snapshot.LastError}";
        }

        var broken = snapshot.Pipelines.Count(p => p.LastOutcome == RunOutcome.Failed);
        var running = snapshot.Pipelines.Count(p => p.IsBusy);
        var text = broken == 0 ? "Monitor: all green" : $"Monitor: {broken} failing";
        if (running > 0)
        {
            text += $", {running} running";
        }

        return snapshot.LastError is null ? text : $"{text}\n{snapshot.LastError}";
    }

    private NativeMenu BuildMenu(MonitorSnapshot snapshot)
    {
        var menu = new NativeMenu();

        if (_configError is not null)
        {
            menu.Add(new NativeMenuItem(_configError) { IsEnabled = false });
            menu.Add(new NativeMenuItemSeparator());
        }

        foreach (var pipeline in snapshot.Pipelines)
        {
            var item = new NativeMenuItem($"{Glyph(pipeline)}  {pipeline.Name}");
            if (pipeline.LatestRun?.WebUrl is { } url)
            {
                item.Click += (_, _) => BrowserLauncher.Open(url);
            }

            menu.Add(item);
        }

        if (snapshot.Pipelines.Count > 0)
        {
            menu.Add(new NativeMenuItemSeparator());
        }

        var show = new NativeMenuItem("Show status…");
        show.Click += (_, _) => ShowStatusWindow();
        menu.Add(show);

        var refresh = new NativeMenuItem("Refresh now") { IsEnabled = _engine is not null };
        refresh.Click += (_, _) => _engine?.RequestRefresh();
        menu.Add(refresh);

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        menu.Add(quit);

        return menu;
    }

    private void ShowStatusWindow()
    {
        _window ??= new StatusWindow();
        _window.Update(_engine?.Current ?? MonitorSnapshot.Empty, _configError);
        _window.Show();
        _window.Activate();
    }

    private static string Glyph(PipelineStatus pipeline)
    {
        var health = pipeline.LastOutcome switch
        {
            RunOutcome.Succeeded => "✔",
            RunOutcome.PartiallySucceeded => "⚠",
            RunOutcome.Failed => "✖",
            _ => "•",
        };
        return pipeline.IsBusy ? $"{health} ⟳" : health;
    }

    private static string Summarize(RunTransition transition) => transition.Kind switch
    {
        TransitionKind.Broke => $"✖ {transition.After.Name} failed",
        TransitionKind.Fixed => $"✔ {transition.After.Name} is fixed",
        _ => transition.After.Name,
    };

    private static string Describe(RunTransition transition)
    {
        var run = transition.After.LastCompletedRun;
        return run is null
            ? string.Empty
            : string.Join(" · ", new[] { run.RunName, run.Branch, run.RequestedBy }.Where(s => !string.IsNullOrEmpty(s)));
    }
}
