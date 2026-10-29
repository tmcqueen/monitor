namespace Monitor.Core.Model;

/// <summary>Identifies a monitored pipeline across all sources.</summary>
public readonly record struct PipelineKey(string SourceId, string PipelineId)
{
    public override string ToString() => $"{SourceId}/{PipelineId}";
}
