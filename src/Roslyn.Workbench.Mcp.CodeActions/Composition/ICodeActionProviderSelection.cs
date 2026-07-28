namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal interface ICodeActionProviderSelection
{
    IReadOnlyList<CodeRefactoringProvider> RefactoringProviders { get; }

    IReadOnlyList<CodeFixProvider> CodeFixProviders { get; }
}
