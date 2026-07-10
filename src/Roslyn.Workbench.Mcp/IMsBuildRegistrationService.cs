
namespace Roslyn.Workbench.Mcp;

internal interface IMsBuildRegistrationService
{
    ComponentStatus EnsureRegistered();

    ComponentStatus CurrentStatus { get; }
}
