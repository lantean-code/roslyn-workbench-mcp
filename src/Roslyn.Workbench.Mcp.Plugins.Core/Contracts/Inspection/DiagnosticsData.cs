namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-diagnostics.
/// </summary>
internal sealed record DiagnosticsData : IQueryResponse
{
    /// <summary>
    /// Gets the returned diagnostics.
    /// </summary>
    [Description("The returned diagnostics.")]
    public BoundedCollection<DiagnosticInfo> Diagnostics { get; init; } = BoundedCollection.Empty<DiagnosticInfo>();
}
