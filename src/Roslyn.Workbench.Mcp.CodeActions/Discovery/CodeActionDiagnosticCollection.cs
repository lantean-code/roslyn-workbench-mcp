namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Holds diagnostics collected for Code Fix discovery together with non-fatal collection warnings.
/// </summary>
internal sealed record CodeActionDiagnosticCollection
{
    /// <summary>
    /// Gets the diagnostics available to Code Fix providers.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets non-fatal analyzer activation and execution warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionDiagnosticCollection"/> class.
    /// </summary>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    public CodeActionDiagnosticCollection(
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyList<string> warnings)
    {
        Diagnostics = diagnostics;
        Warnings = warnings;
    }
}
