namespace Roslyn.Workbench.Mcp.CodeActions.Configuration;

internal sealed class CodeActionExecutionOptions
{
    internal const int _defaultMaximumDiagnosticContextsPerAction = 10;

    public int MaximumDiagnosticContextsPerAction { get; set; } = _defaultMaximumDiagnosticContextsPerAction;

    public TimeSpan ReferenceLifetime { get; set; } = TimeSpan.FromMinutes(5);
}
