using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class ValidatedWorkspaceLoadResult
{
    private ValidatedWorkspaceLoadResult(
        ILoadedWorkspace? workspace,
        Solution? solution,
        WorkspaceProjectTargetFrameworkMap? projectTargetFrameworks,
        ValidatedWorkspaceLoadFailure? failure,
        IReadOnlyList<DiagnosticInfo> diagnostics)
    {
        Workspace = workspace;
        Solution = solution;
        ProjectTargetFrameworks = projectTargetFrameworks;
        Failure = failure;
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }

    public ValidatedWorkspaceLoadFailure? Failure { get; }

    public Solution? Solution { get; }

    public WorkspaceProjectTargetFrameworkMap? ProjectTargetFrameworks { get; }

    public ILoadedWorkspace? Workspace { get; }

    [MemberNotNullWhen(true, nameof(Failure))]
    [MemberNotNullWhen(false, nameof(Solution))]
    [MemberNotNullWhen(false, nameof(ProjectTargetFrameworks))]
    [MemberNotNullWhen(false, nameof(Workspace))]
    public bool HasFailure => Failure is not null;

    public static ValidatedWorkspaceLoadResult Failed(
        ValidatedWorkspaceLoadFailure failure,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null)
    {
        return new ValidatedWorkspaceLoadResult(
            workspace: null,
            solution: null,
            projectTargetFrameworks: null,
            failure,
            diagnostics ?? []);
    }

    public static ValidatedWorkspaceLoadResult Succeeded(
        ILoadedWorkspace workspace,
        Solution solution,
        WorkspaceProjectTargetFrameworkMap projectTargetFrameworks,
        IReadOnlyList<DiagnosticInfo> diagnostics)
    {
        return new ValidatedWorkspaceLoadResult(
            workspace,
            solution,
            projectTargetFrameworks,
            failure: null,
            diagnostics);
    }
}
