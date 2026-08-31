using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Represents either a fully validated workspace load or a classified load failure.
/// </summary>
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

    /// <summary>
    /// Gets diagnostics accumulated while loading and validating the workspace.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }

    /// <summary>
    /// Gets the failure classification, when validation did not succeed.
    /// </summary>
    public ValidatedWorkspaceLoadFailure? Failure { get; }

    /// <summary>
    /// Gets the filtered solution when validation succeeded.
    /// </summary>
    public Solution? Solution { get; }

    /// <summary>
    /// Gets the target-framework identity map when validation succeeded.
    /// </summary>
    public WorkspaceProjectTargetFrameworkMap? ProjectTargetFrameworks { get; }

    /// <summary>
    /// Gets the owned workspace when validation succeeded.
    /// </summary>
    public ILoadedWorkspace? Workspace { get; }

    /// <summary>
    /// Gets a value indicating whether the result represents a failure.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Failure))]
    [MemberNotNullWhen(false, nameof(Solution))]
    [MemberNotNullWhen(false, nameof(ProjectTargetFrameworks))]
    [MemberNotNullWhen(false, nameof(Workspace))]
    public bool HasFailure => Failure is not null;

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="failure">The failure classification.</param>
    /// <param name="diagnostics">The diagnostics associated with the failure.</param>
    /// <returns>A result that represents failure.</returns>
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

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="workspace">The owned loaded workspace.</param>
    /// <param name="solution">The validated and filtered solution.</param>
    /// <param name="projectTargetFrameworks">The target-framework identity map.</param>
    /// <param name="diagnostics">The diagnostics accumulated during loading and validation.</param>
    /// <returns>A result that represents successful completion.</returns>
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
