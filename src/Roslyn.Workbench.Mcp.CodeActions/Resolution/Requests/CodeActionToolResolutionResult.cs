using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

internal sealed record CodeActionToolResolutionResult<TValue, TResponse>
{
    public TValue? Value { get; }

    public CodeActionExecutionResult<TResponse>? Rejection { get; }

    [MemberNotNullWhen(true, nameof(Rejection))]
    [MemberNotNullWhen(false, nameof(Value))]
    public bool HasRejection => Rejection is not null;

    internal CodeActionToolResolutionResult(
        TValue? value,
        CodeActionExecutionResult<TResponse>? rejection)
    {
        Value = value;
        Rejection = rejection;
    }
}

internal static class CodeActionToolResolutionResult
{
    public static CodeActionToolResolutionResult<TValue, TResponse> Resolved<TValue, TResponse>(TValue value)
    {
        return new CodeActionToolResolutionResult<TValue, TResponse>(value, rejection: null);
    }

    public static CodeActionToolResolutionResult<TValue, TResponse> Rejected<TValue, TResponse>(
        CodeActionExecutionResult<TResponse> rejection)
    {
        return new CodeActionToolResolutionResult<TValue, TResponse>(value: default, rejection);
    }
}
