
namespace Roslyn.Workbench.Mcp;

internal sealed class MsBuildRegistrationService : IMsBuildRegistrationService
{
    public ComponentStatus EnsureRegistered()
    {
        return MsBuildRegistration.EnsureRegistered();
    }

    public ComponentStatus CurrentStatus => MsBuildRegistration.CurrentStatus;
}
