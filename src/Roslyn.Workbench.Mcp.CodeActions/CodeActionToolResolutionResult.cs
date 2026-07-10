using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed record CodeActionToolResolutionResult<TValue, TResponse>
{
    public TValue? Value { get; init; }

    public CodeActionExecutionResult<TResponse>? Rejection { get; init; }

    [MemberNotNullWhen(true, nameof(Rejection))]
    [MemberNotNullWhen(false, nameof(Value))]
    public bool HasRejection => Rejection is not null;
}
