using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

/// <summary>
/// Represents either a rediscovered Code Action with its source context or a rejection.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
internal sealed record CodeActionResolution<T>
{
    /// <summary>
    /// Gets the rejection returned instead of a resolved Code Action.
    /// </summary>
    public CodeActionExecutionResult<T>? Rejection { get; }

    /// <summary>
    /// Gets the reason Code Action resolution failed.
    /// </summary>
    public CodeActionResolutionFailureKind FailureKind { get; }

    /// <summary>
    /// Gets the resolved Code Action when resolution succeeds.
    /// </summary>
    public DiscoveredCodeAction? Action { get; }

    /// <summary>
    /// Gets the document against which the Code Action was resolved.
    /// </summary>
    public Document? Document { get; }

    /// <summary>
    /// Gets the source span to which the Code Action applies.
    /// </summary>
    public TextSpan Span { get; }

    /// <summary>
    /// Gets the replayable reference used to resolve the Code Action.
    /// </summary>
    public CodeActionReference? Reference { get; }

    /// <summary>
    /// Gets a value indicating whether resolution produced a rejection instead of an action.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Rejection))]
    [MemberNotNullWhen(false, nameof(Action))]
    [MemberNotNullWhen(false, nameof(Document))]
    [MemberNotNullWhen(false, nameof(Reference))]
    public bool HasRejection => Rejection is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionResolution{T}"/> class.
    /// </summary>
    /// <param name="rejection">The result that explains why the operation was rejected.</param>
    /// <param name="failureKind">The reason Code Action resolution failed.</param>
    /// <param name="action">The rediscovered Code Action.</param>
    /// <param name="document">The document containing the action target.</param>
    /// <param name="span">The source span to which the operation applies.</param>
    /// <param name="reference">The temporary reference used to rediscover the action.</param>
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

/// <summary>
/// Creates successful and rejected Code Action resolution results.
/// </summary>
internal static class CodeActionResolution
{
    /// <summary>
    /// Creates a successful Code Action resolution from the resolved action and source context.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="action">The rediscovered Code Action.</param>
    /// <param name="document">The document containing the action target.</param>
    /// <param name="span">The source span to which the operation applies.</param>
    /// <param name="reference">The temporary reference used to rediscover the action.</param>
    /// <returns>A successful resolution containing the Code Action and its source context.</returns>
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

    /// <summary>
    /// Creates a Code Action resolution that was rejected.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="rejection">The result that explains why the operation was rejected.</param>
    /// <param name="failureKind">The reason Code Action resolution failed.</param>
    /// <returns>A resolution containing the supplied rejection and no resolved action.</returns>
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
