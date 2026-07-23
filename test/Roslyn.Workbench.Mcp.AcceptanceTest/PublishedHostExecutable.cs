namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal static class PublishedHostExecutable
{
    public const string EnvironmentVariableName = "ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH";

    public static string ResolveFromEnvironment()
    {
        return Resolve(Environment.GetEnvironmentVariable(EnvironmentVariableName));
    }

    public static string Resolve(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                $"Set {EnvironmentVariableName} to the absolute path of a published Roslyn.Workbench.Mcp executable. "
                + "See the acceptance-test README for explicit Debug and Release commands.");
        }

        if (!Path.IsPathFullyQualified(configuredPath))
        {
            throw new InvalidOperationException(
                $"The published Host executable configured by {EnvironmentVariableName} must be an absolute path: '{configuredPath}'.");
        }

        var executablePath = Path.GetFullPath(configuredPath);

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                $"The published Host executable configured by {EnvironmentVariableName} does not exist: '{executablePath}'. "
                + "Publish the Host for the intended configuration, then set the variable to that executable.",
                executablePath);
        }

        return executablePath;
    }
}
