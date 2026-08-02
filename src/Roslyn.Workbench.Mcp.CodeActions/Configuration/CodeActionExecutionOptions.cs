namespace Roslyn.Workbench.Mcp.CodeActions.Configuration;

internal sealed class CodeActionExecutionOptions
{
    public const int DefaultMaximumDiagnosticContextsPerAction = 10;

    public int MaximumDiagnosticContextsPerAction { get; set; } = DefaultMaximumDiagnosticContextsPerAction;

    public TimeSpan ReferenceLifetime { get; set; } = TimeSpan.FromMinutes(5);
}
