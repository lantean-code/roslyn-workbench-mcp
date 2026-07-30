using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionInfoFactory
{
    bool TryCreate(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        Document document,
        ResolvedLocation location,
        [NotNullWhen(true)] out CodeActionListItem? item);
}
