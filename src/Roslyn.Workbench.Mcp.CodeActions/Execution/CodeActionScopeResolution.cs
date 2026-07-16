using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed record CodeActionScopeResolution
{
    public CodeActionExecutionResult<WorkspaceMutationCandidate>? Rejection { get; init; }

    public IReadOnlyList<Document> Documents { get; init; } = [];

    public IReadOnlyList<Project> Projects { get; init; } = [];

    [MemberNotNullWhen(true, nameof(Rejection))]
    public bool HasRejection => Rejection is not null;
}
