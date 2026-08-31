namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Contexts;

/// <summary>
/// Marks a Code Action context acquired from the active transaction.
/// </summary>
internal interface ICodeActionMutationContext : ICodeActionExecutionContext
{
}
