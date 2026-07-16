namespace Roslyn.Workbench.Mcp.Hosting;

internal sealed record CodeActionCatalogSnapshot
{
    public IReadOnlyList<IRegisteredCodeActionTool> Tools { get; init; } = [];
}
