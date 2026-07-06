namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Composition;

internal interface ICodeActionRuntimeComposer
{
    CodeActionRuntime Compose(CodeActionRuntimeOptions options);
}
