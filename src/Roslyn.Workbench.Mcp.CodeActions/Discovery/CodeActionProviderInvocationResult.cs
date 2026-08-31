using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Represents either a provider-produced value or an isolated provider failure.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
internal sealed class CodeActionProviderInvocationResult<T> where T : class
{
    /// <summary>
    /// Gets the provider-produced value when invocation succeeded.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the normalized provider failure when invocation failed.
    /// </summary>
    public CodeActionProviderFailure? Failure { get; }

    /// <summary>
    /// Gets a value indicating whether the operation completed successfully.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsSuccessful => Value is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionProviderInvocationResult{T}"/> class.
    /// </summary>
    /// <param name="value">The value returned by a successful provider invocation.</param>
    /// <param name="failure">The failure that prevents the operation from continuing.</param>
    internal CodeActionProviderInvocationResult(
        T? value,
        CodeActionProviderFailure? failure)
    {
        Value = value;
        Failure = failure;
    }
}

/// <summary>
/// Describes the result of Code Action provider invocation.
/// </summary>
internal static class CodeActionProviderInvocationResult
{
    /// <summary>
    /// Creates a result that represents successful completion.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to process.</param>
    /// <returns>A result that represents successful completion.</returns>
    public static CodeActionProviderInvocationResult<T> Success<T>(T value) where T : class
    {
        return new CodeActionProviderInvocationResult<T>(value, failure: null);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="failure">The failure that prevents the operation from continuing.</param>
    /// <returns>A result that represents failure.</returns>
    public static CodeActionProviderInvocationResult<T> Failed<T>(CodeActionProviderFailure failure) where T : class
    {
        return new CodeActionProviderInvocationResult<T>(value: null, failure);
    }
}
