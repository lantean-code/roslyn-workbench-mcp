using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed record CodeActionScopeResolution
{
    public CodeActionExecutionResult<WorkspaceMutationCandidate>? Rejection { get; }

    public IReadOnlyList<Document> Documents { get; }

    public IReadOnlyList<Project> Projects { get; }

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

    public static CodeActionScopeResolution Resolved(
        IReadOnlyList<Document> documents,
        IReadOnlyList<Project>? projects = null)
    {
        return new CodeActionScopeResolution(
            rejection: null,
            documents,
            projects ?? []);
    }

    public static CodeActionScopeResolution Rejected(
        CodeActionExecutionResult<WorkspaceMutationCandidate> rejection)
    {
        return new CodeActionScopeResolution(rejection, documents: [], projects: []);
    }
}
