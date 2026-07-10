using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp;

internal interface IRecoveryStatusReader
{
    IReadOnlyList<RecoveryStatus> GetStatuses(string stateDirectory);
}
