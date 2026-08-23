using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionProviderInvocationResult<T> where T : class
{
    public T? Value { get; }

    public CodeActionProviderFailure? Failure { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsSuccessful => Value is not null;

    internal CodeActionProviderInvocationResult(
        T? value,
        CodeActionProviderFailure? failure)
    {
        Value = value;
        Failure = failure;
    }
}

internal static class CodeActionProviderInvocationResult
{
    public static CodeActionProviderInvocationResult<T> Success<T>(T value) where T : class
    {
        return new CodeActionProviderInvocationResult<T>(value, failure: null);
    }

    public static CodeActionProviderInvocationResult<T> Failed<T>(CodeActionProviderFailure failure) where T : class
    {
        return new CodeActionProviderInvocationResult<T>(value: null, failure);
    }
}
