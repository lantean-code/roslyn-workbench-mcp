using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal sealed record ScopedCodeFixCandidateResolution
{
    public ScopedCodeFixCandidateResolutionOutcome Outcome { get; }

    public ScopedCodeFixCandidate? Candidate { get; }

    public string? Message { get; }

    [MemberNotNullWhen(true, nameof(Candidate))]
    public bool IsResolved => Outcome == ScopedCodeFixCandidateResolutionOutcome.Resolved;

    [MemberNotNullWhen(true, nameof(Message))]
    public bool HasFailure => Outcome is ScopedCodeFixCandidateResolutionOutcome.Unavailable
        or ScopedCodeFixCandidateResolutionOutcome.Ambiguous;

    private ScopedCodeFixCandidateResolution(
        ScopedCodeFixCandidateResolutionOutcome outcome,
        ScopedCodeFixCandidate? candidate,
        string? message)
    {
        Outcome = outcome;
        Candidate = candidate;
        Message = message;
    }

    public static ScopedCodeFixCandidateResolution Resolved(ScopedCodeFixCandidate candidate)
    {
        return new ScopedCodeFixCandidateResolution(
            ScopedCodeFixCandidateResolutionOutcome.Resolved,
            candidate,
            message: null);
    }

    public static ScopedCodeFixCandidateResolution NoDiagnostics()
    {
        return new ScopedCodeFixCandidateResolution(
            ScopedCodeFixCandidateResolutionOutcome.NoDiagnostics,
            candidate: null,
            message: null);
    }

    public static ScopedCodeFixCandidateResolution Unavailable(string message)
    {
        return new ScopedCodeFixCandidateResolution(
            ScopedCodeFixCandidateResolutionOutcome.Unavailable,
            candidate: null,
            message);
    }

    public static ScopedCodeFixCandidateResolution Ambiguous(string message)
    {
        return new ScopedCodeFixCandidateResolution(
            ScopedCodeFixCandidateResolutionOutcome.Ambiguous,
            candidate: null,
            message);
    }
}
