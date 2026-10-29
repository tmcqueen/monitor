namespace Monitor.Core.Events;

/// <summary>
/// A doorbell: "something changed, go look." Events are never trusted as the source of truth;
/// the engine always re-reads current state from the provider's REST API.
/// </summary>
/// <param name="SourceId">The source the event belongs to.</param>
/// <param name="PipelineId">The pipeline the event mentions, when known.</param>
/// <param name="RunId">The run the event mentions, when known.</param>
public sealed record RefreshRequest(string SourceId, string? PipelineId = null, string? RunId = null);
