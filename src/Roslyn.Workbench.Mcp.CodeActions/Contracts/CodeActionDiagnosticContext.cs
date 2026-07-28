namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Describes a diagnostic associated with a discovered code fix.
/// </summary>
internal sealed record CodeActionDiagnosticContext
{
    /// <summary>
    /// Gets the diagnostic identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
