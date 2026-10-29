using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Monitor.Core.Model;
using Monitor.Core.Sources;
using Monitor.Core.Sources.AzureDevOps;

namespace Monitor.Core.Tests;

public class AdoRunSourceTests
{
    // Trimmed from a real Builds - List response; newest first, as requested by queryOrder.
    private const string BuildsJson = """
        {
          "count": 4,
          "value": [
            {
              "id": 104, "buildNumber": "20261029.4", "status": "inProgress", "result": null,
              "queueTime": "2026-10-29T10:04:00Z", "sourceBranch": "refs/heads/main",
              "definition": { "id": 12, "name": "web-ci" },
              "requestedFor": { "displayName": "Ada Lovelace" },
              "_links": { "web": { "href": "https://dev.azure.com/org/proj/_build/results?buildId=104" } }
            },
            {
              "id": 103, "buildNumber": "20261029.3", "status": "completed", "result": "failed",
              "queueTime": "2026-10-29T10:03:00Z", "finishTime": "2026-10-29T10:13:00Z",
              "sourceBranch": "refs/heads/main",
              "definition": { "id": 12, "name": "web-ci" },
              "requestedFor": { "displayName": "Grace Hopper" },
              "_links": { "web": { "href": "https://dev.azure.com/org/proj/_build/results?buildId=103" } }
            },
            {
              "id": 102, "buildNumber": "20261029.2", "status": "completed", "result": "succeeded",
              "queueTime": "2026-10-29T10:02:00Z", "sourceBranch": "refs/heads/main",
              "definition": { "id": 12, "name": "web-ci" }
            },
            {
              "id": 90, "buildNumber": "20261028.1", "status": "notStarted",
              "queueTime": "2026-10-29T09:00:00Z", "sourceBranch": "refs/pull/7/merge",
              "definition": { "id": 15, "name": "api-ci" }
            }
          ]
        }
        """;

    private static readonly AdoOptions Options = new("org", "my project", "secret-pat", [12, 15, 99], "refs/heads/main");

    [Fact]
    public async Task FoldsRecentBuildsIntoPipelineStatuses()
    {
        var source = CreateSource(_ => Json(BuildsJson), out _);

        var statuses = await source.GetPipelineStatusesAsync(CancellationToken.None);

        Assert.Equal(3, statuses.Count);

        var web = statuses[0];
        Assert.Equal(new PipelineKey("ado:org/my project", "12"), web.Key);
        Assert.Equal("web-ci", web.Name);
        Assert.Equal(RunActivity.Running, web.Activity);
        Assert.Equal("104", web.LatestRun?.RunId);
        Assert.Equal("103", web.LastCompletedRun?.RunId);
        Assert.Equal(RunOutcome.Failed, web.LastOutcome);
        Assert.Equal("main", web.LastCompletedRun?.Branch);
        Assert.Equal("Grace Hopper", web.LastCompletedRun?.RequestedBy);
        Assert.Equal(new Uri("https://dev.azure.com/org/proj/_build/results?buildId=103"), web.LastCompletedRun?.WebUrl);

        var api = statuses[1];
        Assert.Equal(RunActivity.Queued, api.Activity);
        Assert.Null(api.LastCompletedRun);
        Assert.Equal(RunOutcome.Unknown, api.LastOutcome);

        var missing = statuses[2];
        Assert.Equal("Pipeline 99", missing.Name);
        Assert.Null(missing.LatestRun);
    }

    [Fact]
    public async Task SendsBasicAuthAndTheExpectedQuery()
    {
        var source = CreateSource(_ => Json("""{ "count": 0, "value": [] }"""), out var requests);

        await source.GetPipelineStatusesAsync(CancellationToken.None);

        var request = Assert.Single(requests);
        Assert.Equal(
            "https://dev.azure.com/org/my%20project/_apis/build/builds?definitions=12,15,99&queryOrder=queueTimeDescending&maxBuildsPerDefinition=5&branchName=refs%2Fheads%2Fmain&api-version=7.1",
            request.RequestUri!.AbsoluteUri);
        Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
        Assert.Equal(":secret-pat", Encoding.ASCII.GetString(Convert.FromBase64String(request.Headers.Authorization!.Parameter!)));
    }

    [Fact]
    public async Task TooManyRequestsHonorsRetryAfter()
    {
        var source = CreateSource(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
            return response;
        }, out _);

        var ex = await Assert.ThrowsAsync<SourceThrottledException>(() => source.GetPipelineStatusesAsync(CancellationToken.None));
        Assert.Equal(TimeSpan.FromSeconds(42), ex.RetryAfter);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NonAuthoritativeInformation)]
    public async Task RejectedCredentialsAreReportedAsAuthenticationFailures(HttpStatusCode status)
    {
        var source = CreateSource(_ => new HttpResponseMessage(status) { Content = new StringContent("<html>sign in</html>") }, out _);

        await Assert.ThrowsAsync<SourceAuthenticationException>(() => source.GetPipelineStatusesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task NotFoundPointsAtTheOrganizationAndProject()
    {
        var source = CreateSource(_ => new HttpResponseMessage(HttpStatusCode.NotFound), out _);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => source.GetPipelineStatusesAsync(CancellationToken.None));
        Assert.Contains("'org'", ex.Message);
        Assert.Contains("'my project'", ex.Message);
    }

    [Fact]
    public void RequiresAtLeastOneDefinition() =>
        Assert.Throws<ArgumentException>(() => new AdoRunSource(Options with { DefinitionIds = [] }, new HttpClient()));

    private static AdoRunSource CreateSource(Func<HttpRequestMessage, HttpResponseMessage> respond, out List<HttpRequestMessage> requests)
    {
        var handler = new StubHandler(respond);
        requests = handler.Requests;
        return new AdoRunSource(Options, new HttpClient(handler));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
