namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public static class MsBuildTestRegistration
{
    private static readonly Lock _syncRoot = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        lock (_syncRoot)
        {
            if (_registered || MSBuildLocator.IsRegistered)
            {
                _registered = true;
                return;
            }

            MSBuildLocator.RegisterDefaults();
            _registered = true;
        }
    }
}
