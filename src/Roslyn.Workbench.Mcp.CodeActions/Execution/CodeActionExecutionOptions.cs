namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class CodeActionExecutionOptions
{
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(5);
}
