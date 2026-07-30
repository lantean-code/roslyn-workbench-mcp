using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

internal sealed record CodeActionResolution<T>
{
    public CodeActionExecutionResult<T>? Rejection { get; }

    public CodeActionResolutionFailureKind FailureKind { get; }

    public DiscoveredCodeAction? Action { get; }

    public Document? Document { get; }

    public TextSpan Span { get; }

    public CodeActionReference? Reference { get; }

    [MemberNotNullWhen(true, nameof(Rejection))]
    [MemberNotNullWhen(false, nameof(Action))]
    [MemberNotNullWhen(false, nameof(Document))]
    [MemberNotNullWhen(false, nameof(Reference))]
    public bool HasRejection => Rejection is not null;

    internal CodeActionResolution(
        CodeActionExecutionResult<T>? rejection,
        CodeActionResolutionFailureKind failureKind,
        DiscoveredCodeAction? action,
        Document? document,
        TextSpan span,
        CodeActionReference? reference)
    {
        Rejection = rejection;
        FailureKind = failureKind;
        Action = action;
        Document = document;
        Span = span;
        Reference = reference;
    }
}

internal static class CodeActionResolution
{
    public static CodeActionResolution<T> Resolved<T>(
        DiscoveredCodeAction action,
        Document document,
        TextSpan span,
        CodeActionReference reference)
    {
        return new CodeActionResolution<T>(
            rejection: null,
            CodeActionResolutionFailureKind.None,
            action,
            document,
            span,
            reference);
    }

    public static CodeActionResolution<T> Rejected<T>(
        CodeActionExecutionResult<T> rejection,
        CodeActionResolutionFailureKind failureKind = CodeActionResolutionFailureKind.None)
    {
        return new CodeActionResolution<T>(
            rejection,
            failureKind,
            action: null,
            document: null,
            span: default,
            reference: null);
    }
}
