using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

internal sealed record CodeActionApplyResult
{
    public Solution? CandidateSolution { get; }

    public CodeActionApplyFailure? Failure { get; }

    [MemberNotNullWhen(true, nameof(Failure))]
    [MemberNotNullWhen(false, nameof(CandidateSolution))]
    public bool HasFailure => Failure is not null;

    private CodeActionApplyResult(
        Solution? candidateSolution,
        CodeActionApplyFailure? failure)
    {
        CandidateSolution = candidateSolution;
        Failure = failure;
    }

    public static CodeActionApplyResult Applied(Solution candidateSolution)
    {
        return new CodeActionApplyResult(candidateSolution, failure: null);
    }

    public static CodeActionApplyResult Failed(CodeActionApplyFailure failure)
    {
        return new CodeActionApplyResult(candidateSolution: null, failure);
    }

    public static CodeActionApplyResult Failed(
        CodeActionApplyFailureKind kind,
        string message)
    {
        var failure = new CodeActionApplyFailure
        {
            Kind = kind,
            Message = message,
        };

        return Failed(failure);
    }
}
