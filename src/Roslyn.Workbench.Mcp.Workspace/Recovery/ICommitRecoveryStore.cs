using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal interface ICommitRecoveryStore
{
    ValueTask<IReadOnlyList<RecoveryStatus>> GetStatusesAsync(CancellationToken cancellationToken);

    ValueTask WriteStatusAsync(RecoveryStatus status, CancellationToken cancellationToken);

    void DeleteStatus(string commitId);
}
