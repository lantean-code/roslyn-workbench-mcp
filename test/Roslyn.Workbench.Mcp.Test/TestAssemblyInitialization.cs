using System.Runtime.CompilerServices;

namespace Roslyn.Workbench.Mcp.Test;

public static class TestAssemblyInitialization
{
    [ModuleInitializer]
    public static void Initialize()
    {
        MsBuildTestRegistration.EnsureRegistered();
    }
}
