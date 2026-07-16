using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution;

internal sealed record CodeActionResolution<T>
{
    public CodeActionExecutionResult<T>? Rejection { get; init; }

    public CodeActionResolutionFailureKind FailureKind { get; init; }

    public DiscoveredCodeAction? Action { get; init; }

    public CodeActionDescriptorEntry? Descriptor { get; init; }

    public Document? Document { get; init; }

    public TextSpan Span { get; init; }

    [MemberNotNullWhen(true, nameof(Rejection))]
    [MemberNotNullWhen(false, nameof(Action))]
    [MemberNotNullWhen(false, nameof(Descriptor))]
    [MemberNotNullWhen(false, nameof(Document))]
    public bool HasRejection => Rejection is not null;
}
