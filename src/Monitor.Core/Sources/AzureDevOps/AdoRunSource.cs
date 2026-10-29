using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Monitor.Core.Model;

namespace Monitor.Core.Sources.AzureDevOps;

/// <summary>Reads YAML pipeline runs from the Azure DevOps Builds REST API.</summary>
public sealed class AdoRunSource : IRunSource
{
    private const string ApiVersion = "7.1";
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AdoOptions _options;
    private readonly HttpClient _http;
    private readonly AuthenticationHeaderValue _authorization;

    public AdoRunSource(AdoOptions options, HttpClient http)
    {
        if (options.DefinitionIds.Count == 0)
        {
            throw new ArgumentException("At least one pipeline definition ID is required.", nameof(options));
        }

        _options = options;
        _http = http;
        _authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + options.PersonalAccessToken)));
        Id = $"ado:{options.Organization}/{options.Project}";
    }

    public string Id { get; }

    public string DisplayName => $"{_options.Organization}/{_options.Project}";

    public async Task<IReadOnlyList<PipelineStatus>> GetPipelineStatusesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildListUrl());
        request.Headers.Authorization = _authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        ThrowForFailure(response);

        var list = await response.Content.ReadFromJsonAsync<AdoBuildList>(JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Azure DevOps returned an empty build list response.");

        return AdoBuildMapper.ToPipelineStatuses(Id, _options.DefinitionIds, list.Value);
    }

    internal Uri BuildListUrl()
    {
        var query = new StringBuilder()
            .Append("definitions=").Append(string.Join(',', _options.DefinitionIds.Select(i => i.ToString(CultureInfo.InvariantCulture))))
            .Append("&queryOrder=queueTimeDescending")
            .Append("&maxBuildsPerDefinition=").Append(_options.RunsPerPipeline.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(_options.BranchName))
        {
            query.Append("&branchName=").Append(Uri.EscapeDataString(_options.BranchName));
        }

        query.Append("&api-version=").Append(ApiVersion);

        var path = $"{Uri.EscapeDataString(_options.Organization)}/{Uri.EscapeDataString(_options.Project)}/_apis/build/builds";
        return new Uri(_options.BaseUrl, $"{path}?{query}");
    }

    private void ThrowForFailure(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            // Azure DevOps can answer an invalid PAT with a 203 and its sign-in page instead of a 401.
            if (response.StatusCode == HttpStatusCode.NonAuthoritativeInformation)
            {
                throw new SourceAuthenticationException("Azure DevOps rejected the personal access token.");
            }

            return;
        }

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new SourceAuthenticationException(
                    $"Azure DevOps returned {(int)response.StatusCode}. Check that the token is valid and has the Build (Read) scope.");

            case HttpStatusCode.TooManyRequests:
            case HttpStatusCode.ServiceUnavailable when response.Headers.RetryAfter is not null:
                var retryAfter = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null)
                    ?? DefaultRetryAfter;
                throw new SourceThrottledException(
                    retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter,
                    $"Azure DevOps is throttling requests; retrying in {retryAfter.TotalSeconds:0}s.");

            case HttpStatusCode.NotFound:
                throw new HttpRequestException(
                    $"Azure DevOps returned 404. Check the organization ('{_options.Organization}') and project ('{_options.Project}') names.",
                    null,
                    response.StatusCode);

            default:
                response.EnsureSuccessStatusCode();
                break;
        }
    }
}
