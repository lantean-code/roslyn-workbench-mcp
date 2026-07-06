using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceSelectionResult
{
    public WorkspaceSelection? Selection { get; init; }

    public WorkspaceOperationError? Error { get; init; }

    [MemberNotNullWhen(true, nameof(Selection))]
    public bool HasSelection => Selection is not null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Error is not null;
}
