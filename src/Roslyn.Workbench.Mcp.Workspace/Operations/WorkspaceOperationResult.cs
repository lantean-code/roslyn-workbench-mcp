using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Operations;

internal sealed class WorkspaceOperationResult<TOutcome>
{
    public WorkspaceOperationStatus Status { get; init; }

    public WorkspaceOperationContext Context { get; init; } = new();

    public TOutcome? Data { get; init; }

    public WorkspaceOperationError? Error { get; init; }

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];

    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];
}
