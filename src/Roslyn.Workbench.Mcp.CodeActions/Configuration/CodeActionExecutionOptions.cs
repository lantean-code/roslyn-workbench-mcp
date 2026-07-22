namespace Roslyn.Workbench.Mcp.CodeActions.Configuration;

internal sealed class CodeActionExecutionOptions
{
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(5);
}
