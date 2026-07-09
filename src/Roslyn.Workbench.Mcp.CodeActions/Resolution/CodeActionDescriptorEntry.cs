using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution;

internal sealed record CodeActionDescriptorEntry
{
    public bool IsVisible { get; init; } = true;

    public CodeActionExecutionMode ExecutionMode { get; init; }

    public string? ExecutorTool { get; init; }

    public string? DescribeTool { get; init; }

    public string? UnsupportedReasonCode { get; init; }

    public IReadOnlyList<string>? Requirements { get; init; }

    public CodeActionDescriptorContextKind ContextKind { get; init; }

    public string? Message { get; init; }
}
