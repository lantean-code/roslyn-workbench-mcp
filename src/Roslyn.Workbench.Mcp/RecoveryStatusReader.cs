using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp;

internal sealed class RecoveryStatusReader : IRecoveryStatusReader
{
    public IReadOnlyList<RecoveryStatus> GetStatuses(string stateDirectory)
    {
        return CommitRecoveryStore.GetStatuses(stateDirectory);
    }
}
