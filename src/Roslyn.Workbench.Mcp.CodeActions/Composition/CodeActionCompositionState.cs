namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed record CodeActionCompositionState
{
    public CodeActionCompositionStatus Status { get; }

    public HostServices? WorkspaceHostServices { get; }

    public IReadOnlyList<CodeRefactoringProvider> RefactoringProviders { get; }

    public IReadOnlyList<CodeFixProvider> CodeFixProviders { get; }

    private CodeActionCompositionState(
        CodeActionCompositionStatus status,
        HostServices? workspaceHostServices,
        IReadOnlyList<CodeRefactoringProvider> refactoringProviders,
        IReadOnlyList<CodeFixProvider> codeFixProviders)
    {
        Status = status;
        WorkspaceHostServices = workspaceHostServices;
        RefactoringProviders = refactoringProviders;
        CodeFixProviders = codeFixProviders;
    }

    public static CodeActionCompositionState Available(
        HostServices workspaceHostServices,
        IReadOnlyList<CodeRefactoringProvider> refactoringProviders,
        IReadOnlyList<CodeFixProvider> codeFixProviders,
        string? version,
        string? message)
    {
        return new CodeActionCompositionState(
            CodeActionCompositionStatus.Available(version, message),
            workspaceHostServices,
            refactoringProviders,
            codeFixProviders);
    }

    public static CodeActionCompositionState Unavailable(string message)
    {
        return new CodeActionCompositionState(
            CodeActionCompositionStatus.Unavailable(message),
            workspaceHostServices: null,
            refactoringProviders: [],
            codeFixProviders: []);
    }
}
