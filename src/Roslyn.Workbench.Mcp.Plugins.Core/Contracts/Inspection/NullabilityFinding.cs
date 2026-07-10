using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one nullability analysis finding.
/// </summary>
public sealed record NullabilityFinding
{
    /// <summary>
    /// Gets the projected diagnostic.
    /// </summary>
    public DiagnosticInfo? Diagnostic { get; init; }
}
