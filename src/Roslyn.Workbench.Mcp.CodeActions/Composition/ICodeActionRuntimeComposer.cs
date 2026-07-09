namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal interface ICodeActionRuntimeComposer
{
    CodeActionRuntime Compose(CodeActionRuntimeOptions options);
}
