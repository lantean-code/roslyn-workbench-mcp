namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionInfoFactory
{
    CodeActionInfo Create(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        Document document,
        TextSpan span,
        CodeActionDescriptorEntry descriptor);
}
