using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp;

internal interface IMsBuildRegistrationService
{
    ComponentStatus EnsureRegistered();

    ComponentStatus CurrentStatus { get; }
}
