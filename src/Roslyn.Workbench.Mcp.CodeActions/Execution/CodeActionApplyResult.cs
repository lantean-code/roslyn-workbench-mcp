using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed record CodeActionApplyResult
{
    public Solution? CandidateSolution { get; init; }

    public CodeActionExecutionResult<WorkspaceMutationProposal>? Rejection { get; init; }

    [MemberNotNullWhen(true, nameof(Rejection))]
    [MemberNotNullWhen(false, nameof(CandidateSolution))]
    public bool HasRejection
    {
        get
        {
            return Rejection is not null;
        }
    }
}
