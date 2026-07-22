namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

internal sealed record CodeActionToolBehavior
{
    public bool Destructive { get; init; }
}
