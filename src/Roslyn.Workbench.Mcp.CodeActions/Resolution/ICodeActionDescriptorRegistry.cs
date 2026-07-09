namespace Roslyn.Workbench.Mcp.CodeActions.Resolution;

internal interface ICodeActionDescriptorRegistry
{
    CodeActionDescriptorEntry Classify(CodeAction action, string providerId, string title);
}
