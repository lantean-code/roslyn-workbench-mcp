namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Resolution;

internal interface ICodeActionDescriptorRegistry
{
    CodeActionDescriptorEntry Classify(CodeAction action, string providerId, string title);
}
