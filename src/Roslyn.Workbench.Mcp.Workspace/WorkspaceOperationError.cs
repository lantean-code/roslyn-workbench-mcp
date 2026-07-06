using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class WorkspaceOperationError
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public RequiredAction? RequiredAction { get; init; }
}
