using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Monitor.Core.Model;

namespace Monitor.Desktop.Tray;

/// <summary>Draws the tray icon at runtime: a dot colored by health, with a blue ring while busy.</summary>
internal static class TrayIconRenderer
{
    private const int Size = 64;

    private static readonly Dictionary<(OverallHealth, bool), WindowIcon> Cache = [];

    public static WindowIcon Render(AggregateStatus status)
    {
        var key = (status.Health, status.IsBusy);
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var bitmap = new RenderTargetBitmap(new PixelSize(Size, Size), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            var center = new Point(Size / 2.0, Size / 2.0);
            if (status.IsBusy)
            {
                context.DrawEllipse(null, new Pen(StatusColors.Busy, 7), center, Size / 2.0 - 4, Size / 2.0 - 4);
                context.DrawEllipse(StatusColors.For(status.Health), null, center, Size / 2.0 - 13, Size / 2.0 - 13);
            }
            else
            {
                context.DrawEllipse(StatusColors.For(status.Health), null, center, Size / 2.0 - 4, Size / 2.0 - 4);
            }
        }

        var icon = new WindowIcon(bitmap);
        Cache[key] = icon;
        return icon;
    }
}

internal static class StatusColors
{
    public static readonly IBrush Healthy = new SolidColorBrush(Color.FromRgb(0x2E, 0x9E, 0x44));
    public static readonly IBrush Warning = new SolidColorBrush(Color.FromRgb(0xF2, 0xA9, 0x00));
    public static readonly IBrush Broken = new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38));
    public static readonly IBrush Unknown = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
    public static readonly IBrush Busy = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));

    public static IBrush For(OverallHealth health) => health switch
    {
        OverallHealth.Healthy => Healthy,
        OverallHealth.Warning => Warning,
        OverallHealth.Broken => Broken,
        _ => Unknown,
    };

    public static IBrush For(RunOutcome outcome) => outcome switch
    {
        RunOutcome.Succeeded => Healthy,
        RunOutcome.PartiallySucceeded => Warning,
        RunOutcome.Failed => Broken,
        _ => Unknown,
    };
}
