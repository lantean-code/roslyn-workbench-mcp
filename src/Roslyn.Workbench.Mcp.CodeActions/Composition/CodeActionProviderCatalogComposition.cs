namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed record CodeActionProviderCatalogComposition
{
    public required CodeActionProviderCatalogStatus Status { get; init; }

    public HostServices? WorkspaceHostServices { get; init; }

    public IReadOnlyList<CodeRefactoringProvider> RefactoringProviders { get; init; } = [];

    public IReadOnlyList<CodeFixProvider> CodeFixProviders { get; init; } = [];
}
