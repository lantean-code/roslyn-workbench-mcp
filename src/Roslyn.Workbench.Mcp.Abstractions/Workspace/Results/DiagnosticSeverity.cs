namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the severity level of a diagnostic.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// The diagnostic is hidden.
    /// </summary>
    Hidden,

    /// <summary>
    /// The diagnostic is informational.
    /// </summary>
    Info,

    /// <summary>
    /// The diagnostic is a warning.
    /// </summary>
    Warning,

    /// <summary>
    /// The diagnostic is an error.
    /// </summary>
    Error,
}
