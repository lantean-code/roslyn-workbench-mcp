using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp;

internal interface IRecoveryStatusReader
{
    IReadOnlyList<RecoveryStatus> GetStatuses(string stateDirectory);
}
