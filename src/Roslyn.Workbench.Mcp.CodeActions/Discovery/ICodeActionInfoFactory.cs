namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionInfoFactory
{
    CodeActionInfoCreationResult Create(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        Document document,
        ResolvedLocation location);
}
