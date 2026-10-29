using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Monitor.Core.Model;
using Monitor.Desktop.Tray;

namespace Monitor.Desktop;

internal sealed record PipelineRow(string Name, string Detail, IBrush Dot, IBrush? Ring, Uri? Url);

internal sealed partial class StatusWindow : Window
{
    public StatusWindow()
    {
        InitializeComponent();
    }

    public void Update(MonitorSnapshot snapshot, string? configError)
    {
        Pipelines.ItemsSource = snapshot.Pipelines.Select(ToRow).ToList();

        var lines = new List<string>();
        if (configError is not null)
        {
            lines.Add(configError);
        }

        if (snapshot.LastUpdated is { } updated)
        {
            lines.Add($"Updated {updated.ToLocalTime():T}");
        }

        if (snapshot.LastError is not null)
        {
            lines.Add(snapshot.LastError);
        }

        Footer.Text = string.Join(Environment.NewLine, lines);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Closing the window just hides it; the app lives in the tray.
        if (e.CloseReason is not WindowCloseReason.ApplicationShutdown and not WindowCloseReason.OSShutdown)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private void OnPipelineDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Pipelines.SelectedItem is PipelineRow { Url: { } url })
        {
            BrowserLauncher.Open(url);
        }
    }

    private static PipelineRow ToRow(PipelineStatus pipeline)
    {
        var run = pipeline.LatestRun;
        var parts = new List<string>();
        if (run is not null)
        {
            parts.Add(run.RunName);
            if (run.Branch is not null)
            {
                parts.Add(run.Branch);
            }

            if (run.RequestedBy is not null)
            {
                parts.Add(run.RequestedBy);
            }
        }

        parts.Add(pipeline.Activity switch
        {
            RunActivity.Running => "running",
            RunActivity.Queued => "queued",
            _ => Describe(pipeline.LastCompletedRun),
        });

        return new PipelineRow(
            pipeline.Name,
            string.Join(" · ", parts),
            StatusColors.For(pipeline.LastOutcome),
            pipeline.IsBusy ? StatusColors.Busy : null,
            run?.WebUrl);
    }

    private static string Describe(PipelineRun? completed) => completed switch
    {
        null => "no runs",
        { FinishedAt: { } finished } => $"{Label(completed.Outcome)} {Ago(finished)}",
        _ => Label(completed.Outcome),
    };

    private static string Label(RunOutcome outcome) => outcome switch
    {
        RunOutcome.Succeeded => "Succeeded",
        RunOutcome.PartiallySucceeded => "Partially succeeded",
        RunOutcome.Failed => "Failed",
        RunOutcome.Canceled => "Canceled",
        _ => "Unknown",
    };

    private static string Ago(DateTimeOffset time)
    {
        var age = DateTimeOffset.UtcNow - time;
        return age.TotalMinutes < 1 ? "just now"
            : age.TotalHours < 1 ? $"{(int)age.TotalMinutes}m ago"
            : age.TotalDays < 1 ? $"{(int)age.TotalHours}h ago"
            : $"{(int)age.TotalDays}d ago";
    }
}
