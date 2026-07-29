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
        var status = CodeActionCompositionStatus.Available(version, message);

        return new CodeActionCompositionState(
            status,
            workspaceHostServices,
            refactoringProviders,
            codeFixProviders);
    }

    public static CodeActionCompositionState Unavailable(string message)
    {
        var status = CodeActionCompositionStatus.Unavailable(message);

        return new CodeActionCompositionState(
            status,
            workspaceHostServices: null,
            refactoringProviders: [],
            codeFixProviders: []);
    }
}
