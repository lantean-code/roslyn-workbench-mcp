using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceStatusOutcome
{
    public WorkspaceLifecycleState State { get; init; }

    public WorkspaceIdentity Workspace { get; init; } = new();

    public int ProjectCount { get; init; }

    public int DocumentCount { get; init; }

    public IReadOnlyList<DiagnosticInfo>? LoadDiagnostics { get; init; }

    public TransactionInfo? Transaction { get; init; }

    public bool ReloadRequired { get; init; }
}
