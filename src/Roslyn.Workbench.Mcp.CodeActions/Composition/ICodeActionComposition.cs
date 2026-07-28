namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal interface ICodeActionComposition
{
    CodeActionCompositionStatus Status { get; }

    HostServices? WorkspaceHostServices { get; }

    IReadOnlyList<CodeRefactoringProvider> RefactoringProviders { get; }

    IReadOnlyList<CodeFixProvider> CodeFixProviders { get; }
}
