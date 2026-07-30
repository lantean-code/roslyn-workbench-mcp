namespace Roslyn.Workbench.Mcp.Hosting;

internal interface IMsBuildRegistrationService
{
    ComponentStatus EnsureRegistered();

    ComponentStatus CurrentStatus { get; }
}
