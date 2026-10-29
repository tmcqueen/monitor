using System.Text.Json.Serialization;

namespace Monitor.Core.Sources.AzureDevOps;

// Shapes of the Builds - List response we care about:
// https://learn.microsoft.com/rest/api/azure/devops/build/builds/list

internal sealed record AdoBuildList(int Count, IReadOnlyList<AdoBuild> Value);

internal sealed record AdoBuild(
    int Id,
    string? BuildNumber,
    string? Status,
    string? Result,
    DateTimeOffset? QueueTime,
    DateTimeOffset? FinishTime,
    string? SourceBranch,
    AdoDefinitionRef? Definition,
    AdoIdentityRef? RequestedFor,
    [property: JsonPropertyName("_links")] AdoLinks? Links);

internal sealed record AdoDefinitionRef(int Id, string? Name);

internal sealed record AdoIdentityRef(string? DisplayName);

internal sealed record AdoLinks(AdoLink? Web);

internal sealed record AdoLink(string? Href);
