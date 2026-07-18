namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

/// <summary>
/// Represents a structured diagnostic returned by a tool.
/// </summary>
public sealed record DiagnosticInfo
{
    /// <summary>
    /// Gets the diagnostic identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the diagnostic severity.
    /// </summary>
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional source location of the diagnostic.
    /// </summary>
    public ResolvedLocation? Location { get; init; }
}
