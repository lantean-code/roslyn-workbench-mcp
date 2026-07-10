using System.Runtime.CompilerServices;

using Roslyn.Workbench.Mcp.IntegrationTestSupport;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public static class TestAssemblyInitialization
{
    [ModuleInitializer]
    public static void Initialize()
    {
        MsBuildTestRegistration.EnsureRegistered();
    }
}
