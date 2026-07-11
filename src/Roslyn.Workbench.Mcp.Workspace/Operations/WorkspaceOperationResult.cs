using System.Diagnostics.CodeAnalysis;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Operations;

internal sealed class WorkspaceOperationResult<TOutcome>
{
    public WorkspaceOperationStatus Status { get; init; }

    public WorkspaceOperationContext Context { get; init; } = new();

    public TOutcome? Data { get; init; }

    public WorkspaceOperationError? Error { get; init; }

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];

    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];

    [MemberNotNullWhen(true, nameof(Data))]
    public bool HasData => Data is not null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Error is not null;
}
