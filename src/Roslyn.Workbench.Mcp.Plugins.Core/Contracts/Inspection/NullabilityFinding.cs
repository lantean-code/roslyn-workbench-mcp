namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one nullability analysis finding.
/// </summary>
internal sealed record NullabilityFinding
{
    /// <summary>
    /// Gets the projected diagnostic.
    /// </summary>
    public DiagnosticInfo? Diagnostic { get; init; }
}
