namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal static class AcceptanceFailureDiagnostics
{
    private const string RetainRootEnvironmentVariableName = "ROSLYN_WORKBENCH_MCP_ACCEPTANCE_RETAIN_ROOT";

    public static bool IsRetentionEnabled()
    {
        var configuredValue = Environment.GetEnvironmentVariable(RetainRootEnvironmentVariableName);
        return string.Equals(configuredValue, "1", StringComparison.Ordinal)
            || string.Equals(configuredValue, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task WriteAsync(
        string scenarioRoot,
        string command,
        int? exitCode,
        string standardError)
    {
        Directory.CreateDirectory(scenarioRoot);

        var processDetails = $"Command: {command}{Environment.NewLine}Exit code: {exitCode?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}{Environment.NewLine}";
        await File.WriteAllTextAsync(Path.Combine(scenarioRoot, "process.txt"), processDetails);
        await File.WriteAllTextAsync(Path.Combine(scenarioRoot, "server.stderr.log"), standardError);
    }
}
