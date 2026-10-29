using System.Diagnostics;

namespace Monitor.Desktop;

internal static class BrowserLauncher
{
    /// <summary>Opens a URL in the default browser (xdg-open on Linux).</summary>
    public static void Open(Uri url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Could not open {url}: {ex.Message}");
        }
    }
}
