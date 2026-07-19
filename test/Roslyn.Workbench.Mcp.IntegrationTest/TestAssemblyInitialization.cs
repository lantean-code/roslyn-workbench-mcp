using System.Runtime.CompilerServices;

namespace Roslyn.Workbench.Mcp.Test;

internal static class TestAssemblyInitialization
{
    [ModuleInitializer]
    public static void Initialize()
    {
        MsBuildTestRegistration.EnsureRegistered();
    }
}
