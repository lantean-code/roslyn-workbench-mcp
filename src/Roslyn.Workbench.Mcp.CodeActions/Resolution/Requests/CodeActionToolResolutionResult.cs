using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

/// <summary>
/// Represents either a value resolved from a Code Action request or a rejection.
/// </summary>
/// <typeparam name="TValue">The value type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed record CodeActionToolResolutionResult<TValue, TResponse>
{
    /// <summary>
    /// Gets the resolved value when resolution succeeds.
    /// </summary>
    public TValue? Value { get; }

    /// <summary>
    /// Gets the rejection returned when resolution fails.
    /// </summary>
    public CodeActionExecutionResult<TResponse>? Rejection { get; }

    /// <summary>
    /// Gets a value indicating whether resolution produced a rejection instead of a value.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Rejection))]
    [MemberNotNullWhen(false, nameof(Value))]
    public bool HasRejection => Rejection is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionToolResolutionResult{TValue, TResponse}"/> class.
    /// </summary>
    /// <param name="value">The value produced by successful resolution.</param>
    /// <param name="rejection">The result that explains why the operation was rejected.</param>
    internal CodeActionToolResolutionResult(
        TValue? value,
        CodeActionExecutionResult<TResponse>? rejection)
    {
        Value = value;
        Rejection = rejection;
    }
}

/// <summary>
/// Creates successful and rejected request-resolution results.
/// </summary>
internal static class CodeActionToolResolutionResult
{
    /// <summary>
    /// Creates a successful tool resolution containing the resolved value.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="value">The value produced by successful resolution.</param>
    /// <returns>A successful tool resolution containing <paramref name="value"/>.</returns>
    public static CodeActionToolResolutionResult<TValue, TResponse> Resolved<TValue, TResponse>(TValue value)
    {
        return new CodeActionToolResolutionResult<TValue, TResponse>(value, rejection: null);
    }

    /// <summary>
    /// Creates a request-resolution result that was rejected.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="rejection">The result that explains why the operation was rejected.</param>
    /// <returns>A resolution containing the supplied rejection and no resolved value.</returns>
    public static CodeActionToolResolutionResult<TValue, TResponse> Rejected<TValue, TResponse>(
        CodeActionExecutionResult<TResponse> rejection)
    {
        return new CodeActionToolResolutionResult<TValue, TResponse>(value: default, rejection);
    }
}
