using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

/// <summary>
/// Represents either the candidate solution produced by a Code Action or an application failure.
/// </summary>
internal sealed record CodeActionApplyResult
{
    /// <summary>
    /// Gets the candidate solution when evaluation succeeded.
    /// </summary>
    public Solution? CandidateSolution { get; }

    /// <summary>
    /// Gets the reason evaluation failed.
    /// </summary>
    public CodeActionApplyFailure? Failure { get; }

    /// <summary>
    /// Gets a value indicating whether a failure prevented the operation from completing.
    /// </summary>
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

    /// <summary>
    /// Creates a successful result containing the proposed solution.
    /// </summary>
    /// <param name="candidateSolution">The candidate solution containing the proposed changes.</param>
    /// <returns>The successful application result.</returns>
    public static CodeActionApplyResult Applied(Solution candidateSolution)
    {
        return new CodeActionApplyResult(candidateSolution, failure: null);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <param name="failure">The failure that prevents the operation from continuing.</param>
    /// <returns>A result that represents failure.</returns>
    public static CodeActionApplyResult Failed(CodeActionApplyFailure failure)
    {
        return new CodeActionApplyResult(candidateSolution: null, failure);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <param name="kind">The stable failure category.</param>
    /// <param name="message">The user-facing failure explanation.</param>
    /// <returns>A result that represents failure.</returns>
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
