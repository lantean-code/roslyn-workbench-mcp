namespace Roslyn.Workbench.Mcp.Workspace.Configuration;

/// <summary>
/// Provides default values for state directory.
/// </summary>
internal static class StateDirectoryDefaults
{
    private const string _applicationDirectoryName = "roslyn-workbench-mcp";
    private const string _stateDirectoryName = "state";

    /// <summary>
    /// Gets default path.
    /// </summary>
    /// <returns>The default path.</returns>
    public static string GetDefaultPath()
    {
        if (OperatingSystem.IsLinux())
        {
            var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            if (!string.IsNullOrWhiteSpace(stateHome) && Path.IsPathFullyQualified(stateHome))
            {
                return Path.Combine(stateHome, _applicationDirectoryName);
            }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, ".local", _stateDirectoryName, _applicationDirectoryName);
        }

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localApplicationData, _applicationDirectoryName, _stateDirectoryName);
    }
}
