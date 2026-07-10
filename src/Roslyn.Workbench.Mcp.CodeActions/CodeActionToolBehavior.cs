namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed record CodeActionToolBehavior
{
    public bool Destructive { get; init; }
}
