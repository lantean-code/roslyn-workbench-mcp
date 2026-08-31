using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution.FixAll;

/// <summary>
/// Represents either a provider-created Fix All action or a creation failure.
/// </summary>
internal sealed record FixAllActionCreationResult
{
    /// <summary>
    /// Gets the created Fix All action when creation succeeds.
    /// </summary>
    public CodeAction? Action { get; }

    /// <summary>
    /// Gets the reason a Fix All action could not be created.
    /// </summary>
    public FixAllActionCreationFailure? Failure { get; }

    /// <summary>
    /// Gets a value indicating whether a failure prevented the operation from completing.
    /// </summary>
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

    /// <summary>
    /// Creates a successful result containing the Fix All action.
    /// </summary>
    /// <param name="action">The Fix All action returned by the provider.</param>
    /// <returns>A successful result containing <paramref name="action"/>.</returns>
    public static FixAllActionCreationResult Created(CodeAction action)
    {
        return new FixAllActionCreationResult(action, failure: null);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <param name="failure">The failure that prevents the operation from continuing.</param>
    /// <returns>A result that represents failure.</returns>
    public static FixAllActionCreationResult Failed(FixAllActionCreationFailure failure)
    {
        return new FixAllActionCreationResult(action: null, failure);
    }
}
