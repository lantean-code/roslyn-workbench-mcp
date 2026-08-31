using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

/// <summary>
/// Represents either the documents and projects selected by a Code Action scope or a rejection.
/// </summary>
internal sealed record CodeActionScopeResolution
{
    /// <summary>
    /// Gets the rejection returned instead of a resolved scope.
    /// </summary>
    public CodeActionExecutionResult<WorkspaceMutationCandidate>? Rejection { get; }

    /// <summary>
    /// Gets the documents selected for Code Action execution.
    /// </summary>
    public IReadOnlyList<Document> Documents { get; }

    /// <summary>
    /// Gets the projects selected for project-level Code Action execution.
    /// </summary>
    public IReadOnlyList<Project> Projects { get; }

    /// <summary>
    /// Gets a value indicating whether scope resolution produced a rejection.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Rejection))]
    public bool HasRejection => Rejection is not null;

    private CodeActionScopeResolution(
        CodeActionExecutionResult<WorkspaceMutationCandidate>? rejection,
        IReadOnlyList<Document> documents,
        IReadOnlyList<Project> projects)
    {
        Rejection = rejection;
        Documents = documents;
        Projects = projects;
    }

    /// <summary>
    /// Creates a successful scope resolution for the selected documents and projects.
    /// </summary>
    /// <param name="documents">The documents selected for Code Action execution.</param>
    /// <param name="projects">The projects included in the selected workspace scope.</param>
    /// <returns>A successful scope resolution containing the selected workspace items.</returns>
    public static CodeActionScopeResolution Resolved(
        IReadOnlyList<Document> documents,
        IReadOnlyList<Project>? projects = null)
    {
        return new CodeActionScopeResolution(
            rejection: null,
            documents,
            projects ?? []);
    }

    /// <summary>
    /// Creates a rejected scope resolution.
    /// </summary>
    /// <param name="rejection">The result that explains why the operation was rejected.</param>
    /// <returns>A scope resolution containing the supplied rejection and no workspace items.</returns>
    public static CodeActionScopeResolution Rejected(
        CodeActionExecutionResult<WorkspaceMutationCandidate> rejection)
    {
        return new CodeActionScopeResolution(rejection, documents: [], projects: []);
    }
}
