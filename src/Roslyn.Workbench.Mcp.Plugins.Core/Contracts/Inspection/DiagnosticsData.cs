namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-diagnostics.
/// </summary>
internal sealed record DiagnosticsData
{
    /// <summary>
    /// Gets the returned diagnostics.
    /// </summary>
    public BoundedCollection<DiagnosticInfo> Diagnostics { get; init; } = BoundedCollection.Empty<DiagnosticInfo>();
}
