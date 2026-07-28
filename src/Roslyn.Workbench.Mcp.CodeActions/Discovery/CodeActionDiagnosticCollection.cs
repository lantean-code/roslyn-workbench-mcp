namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed record CodeActionDiagnosticCollection
{
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public IReadOnlyList<string> Warnings { get; }

    public CodeActionDiagnosticCollection(
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyList<string> warnings)
    {
        Diagnostics = diagnostics;
        Warnings = warnings;
    }
}
