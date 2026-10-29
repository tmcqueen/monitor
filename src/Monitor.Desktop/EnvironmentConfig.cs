using System.Globalization;
using Monitor.Core.Sources.AzureDevOps;

namespace Monitor.Desktop;

/// <summary>
/// Phase 0 configuration from environment variables. The PAT moves to the Secret Service
/// (KWallet / GNOME Keyring over D-Bus) in phase 1; keeping it in the environment is a stopgap.
/// </summary>
internal static class EnvironmentConfig
{
    public const string Organization = "MONITOR_ADO_ORG";
    public const string Project = "MONITOR_ADO_PROJECT";
    public const string Pat = "MONITOR_ADO_PAT";
    public const string Definitions = "MONITOR_ADO_DEFINITIONS";
    public const string Branch = "MONITOR_ADO_BRANCH";

    public static bool TryLoad(out AdoOptions? options, out string? error)
    {
        options = null;
        var org = Environment.GetEnvironmentVariable(Organization);
        var project = Environment.GetEnvironmentVariable(Project);
        var pat = Environment.GetEnvironmentVariable(Pat);
        var definitions = Environment.GetEnvironmentVariable(Definitions);

        var missing = new[] { (Organization, org), (Project, project), (Pat, pat), (Definitions, definitions) }
            .Where(v => string.IsNullOrWhiteSpace(v.Item2))
            .Select(v => v.Item1)
            .ToList();
        if (missing.Count > 0)
        {
            error = $"Not configured. Set {string.Join(", ", missing)}.";
            return false;
        }

        var ids = new List<int>();
        foreach (var part in definitions!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                error = $"{Definitions} must be a comma-separated list of pipeline IDs; '{part}' is not a number.";
                return false;
            }

            ids.Add(id);
        }

        var branch = Environment.GetEnvironmentVariable(Branch);
        options = new AdoOptions(org!, project!, pat!, ids, string.IsNullOrWhiteSpace(branch) ? null : branch);
        error = null;
        return true;
    }
}
