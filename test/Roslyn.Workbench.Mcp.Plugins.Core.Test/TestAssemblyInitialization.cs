using System.Runtime.CompilerServices;

using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public static class TestAssemblyInitialization
{
    [ModuleInitializer]
    public static void Initialize()
    {
        MsBuildTestRegistration.EnsureRegistered();
    }
}
