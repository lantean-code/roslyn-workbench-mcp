namespace Roslyn.Workbench.Mcp.CodeActions.Resolution;

internal interface ICodeActionDescriptorRegistry
{
    CodeActionProviderCapability GetProviderCapability(string providerId);

    CodeActionDescriptorEntry ResolveActionDependentDescriptor(CodeAction action, string providerId, string title);
}
