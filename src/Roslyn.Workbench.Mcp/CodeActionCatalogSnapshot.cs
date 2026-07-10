namespace Roslyn.Workbench.Mcp;

internal sealed record CodeActionCatalogSnapshot
{
    public IReadOnlyList<IRegisteredCodeActionTool> Tools { get; init; } = [];
}
