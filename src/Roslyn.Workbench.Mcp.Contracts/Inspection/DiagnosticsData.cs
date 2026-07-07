using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-diagnostics.
/// </summary>
[PublishedCollectionResponse(nameof(Diagnostics))]
public sealed record DiagnosticsData
{
    /// <summary>
    /// Gets the returned diagnostics.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets the number of diagnostics returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more diagnostics were available.
    /// </summary>
    public bool HasMore { get; init; }
}
