using Monitor.Core.Model;

namespace Monitor.Core.Sources;

/// <summary>A provider that can report the current state of the pipelines it monitors.</summary>
public interface IRunSource
{
    string Id { get; }

    string DisplayName { get; }

    Task<IReadOnlyList<PipelineStatus>> GetPipelineStatusesAsync(CancellationToken cancellationToken);
}
