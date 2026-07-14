namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal interface ICodeActionProviderCatalog
{
    CodeActionProviderCatalogStatus Status { get; }

    HostServices? WorkspaceHostServices { get; }

    IReadOnlyList<CodeRefactoringProvider> RefactoringProviders { get; }

    IReadOnlyList<CodeFixProvider> CodeFixProviders { get; }
}
