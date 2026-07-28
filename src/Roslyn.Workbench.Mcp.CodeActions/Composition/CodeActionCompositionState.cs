namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed record CodeActionCompositionState
{
    public required CodeActionCompositionStatus Status { get; init; }

    public HostServices? WorkspaceHostServices { get; init; }

    public IReadOnlyList<CodeRefactoringProvider> RefactoringProviders { get; init; } = [];

    public IReadOnlyList<CodeFixProvider> CodeFixProviders { get; init; } = [];
}
