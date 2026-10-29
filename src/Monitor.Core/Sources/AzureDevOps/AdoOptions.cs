namespace Monitor.Core.Sources.AzureDevOps;

/// <param name="Organization">The organization name, as in <c>dev.azure.com/{organization}</c>.</param>
/// <param name="Project">The project name or ID.</param>
/// <param name="PersonalAccessToken">A PAT with the <c>Build (Read)</c> scope.</param>
/// <param name="DefinitionIds">The YAML pipeline (build definition) IDs to monitor.</param>
/// <param name="BranchName">Only consider runs for this branch, such as <c>refs/heads/main</c>. Null means every branch.</param>
public sealed record AdoOptions(
    string Organization,
    string Project,
    string PersonalAccessToken,
    IReadOnlyList<int> DefinitionIds,
    string? BranchName = null)
{
    public static readonly Uri DefaultBaseUrl = new("https://dev.azure.com/");

    public Uri BaseUrl { get; init; } = DefaultBaseUrl;

    /// <summary>
    /// How many recent runs to fetch per pipeline. More than one, so the last completed run is
    /// still visible while newer runs are queued or in progress.
    /// </summary>
    public int RunsPerPipeline { get; init; } = 5;
}
