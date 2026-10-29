namespace Monitor.Core.Sources;

/// <summary>The source asked us to slow down (HTTP 429, or 503 with <c>Retry-After</c>).</summary>
public sealed class SourceThrottledException(TimeSpan retryAfter, string message) : Exception(message)
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}

/// <summary>The source rejected our credentials. Retrying quickly will not help.</summary>
public sealed class SourceAuthenticationException(string message) : Exception(message);
