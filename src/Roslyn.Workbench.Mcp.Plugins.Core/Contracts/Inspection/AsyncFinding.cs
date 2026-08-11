namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one compiler-backed async analysis finding.
/// </summary>
internal sealed record AsyncFinding
{
    /// <summary>
    /// Gets the compiler diagnostic.
    /// </summary>
    public DiagnosticInfo? Diagnostic { get; init; }
}
