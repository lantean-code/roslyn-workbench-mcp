namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionInfoFactory
{
    CodeActionInfo Create(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        Document document,
        TextSpan span,
        CodeActionDescriptorEntry descriptor);
}
