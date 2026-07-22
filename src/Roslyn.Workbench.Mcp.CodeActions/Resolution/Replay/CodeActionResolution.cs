using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

internal sealed record CodeActionResolution<T>
{
    public CodeActionExecutionResult<T>? Rejection { get; }

    public CodeActionResolutionFailureKind FailureKind { get; }

    public DiscoveredCodeAction? Action { get; }

    public CodeActionDescriptorEntry? Descriptor { get; }

    public Document? Document { get; }

    public TextSpan Span { get; }

    [MemberNotNullWhen(true, nameof(Rejection))]
    [MemberNotNullWhen(false, nameof(Action))]
    [MemberNotNullWhen(false, nameof(Descriptor))]
    [MemberNotNullWhen(false, nameof(Document))]
    public bool HasRejection => Rejection is not null;

    private CodeActionResolution(
        CodeActionExecutionResult<T>? rejection,
        CodeActionResolutionFailureKind failureKind,
        DiscoveredCodeAction? action,
        CodeActionDescriptorEntry? descriptor,
        Document? document,
        TextSpan span)
    {
        Rejection = rejection;
        FailureKind = failureKind;
        Action = action;
        Descriptor = descriptor;
        Document = document;
        Span = span;
    }

    public static CodeActionResolution<T> Resolved(
        DiscoveredCodeAction action,
        Document document,
        TextSpan span)
    {
        return new CodeActionResolution<T>(
            rejection: null,
            CodeActionResolutionFailureKind.None,
            action,
            action.Descriptor,
            document,
            span);
    }

    public static CodeActionResolution<T> Rejected(
        CodeActionExecutionResult<T> rejection,
        CodeActionResolutionFailureKind failureKind = CodeActionResolutionFailureKind.None)
    {
        return new CodeActionResolution<T>(
            rejection,
            failureKind,
            action: null,
            descriptor: null,
            document: null,
            span: default);
    }
}
