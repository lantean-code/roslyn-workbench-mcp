using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed record FixAllActionCreationResult
{
    public CodeAction? Action { get; }

    public FixAllActionCreationFailure? Failure { get; }

    [MemberNotNullWhen(true, nameof(Failure))]
    [MemberNotNullWhen(false, nameof(Action))]
    public bool HasFailure => Failure is not null;

    private FixAllActionCreationResult(
        CodeAction? action,
        FixAllActionCreationFailure? failure)
    {
        Action = action;
        Failure = failure;
    }

    public static FixAllActionCreationResult Created(CodeAction action)
    {
        return new FixAllActionCreationResult(action, failure: null);
    }

    public static FixAllActionCreationResult Failed(FixAllActionCreationFailure failure)
    {
        return new FixAllActionCreationResult(action: null, failure);
    }
}
