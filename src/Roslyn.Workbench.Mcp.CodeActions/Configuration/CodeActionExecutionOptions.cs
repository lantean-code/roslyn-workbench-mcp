namespace Roslyn.Workbench.Mcp.CodeActions.Configuration;

internal sealed class CodeActionExecutionOptions
{
    public TimeSpan ReferenceLifetime { get; set; } = TimeSpan.FromMinutes(5);
}
