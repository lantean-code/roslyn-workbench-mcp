using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-diagnostics.
/// </summary>
public sealed record DiagnosticsData
{
    /// <summary>
    /// Gets the returned diagnostics.
    /// </summary>
    public BoundedCollection<DiagnosticInfo> Diagnostics { get; init; } = BoundedCollection<DiagnosticInfo>.Empty();
}
