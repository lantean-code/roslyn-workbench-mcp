using System.Runtime.CompilerServices;

// Compatibility cases create real Roslyn/MSBuild workspaces; two concurrent collections
// retain useful parallelism without multiplying the measured approximately 1.05-GiB peak.
[assembly: CollectionBehavior(MaxParallelThreads = 2)]

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public static class TestAssemblyInitialization
{
    [ModuleInitializer]
    public static void Initialize()
    {
        MsBuildTestRegistration.EnsureRegistered();
    }
}
