namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Describes a diagnostic associated with a discovered code fix.
/// </summary>
internal sealed record CodeActionDiagnosticContext
{
    /// <summary>
    /// Gets the diagnostic identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public required string Message { get; init; }
}
