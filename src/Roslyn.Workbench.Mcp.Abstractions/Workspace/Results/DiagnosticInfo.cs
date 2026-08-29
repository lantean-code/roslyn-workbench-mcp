namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents a structured diagnostic returned by a tool.
/// </summary>
public sealed record DiagnosticInfo
{
    /// <summary>
    /// Gets the diagnostic identifier.
    /// </summary>
    [Description("The diagnostic identifier.")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the diagnostic severity.
    /// </summary>
    [Description("The diagnostic severity.")]
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    [Description("The diagnostic message.")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional source location of the diagnostic.
    /// </summary>
    [Description("The optional source location of the diagnostic.")]
    public ResolvedLocation? Location { get; init; }
}
