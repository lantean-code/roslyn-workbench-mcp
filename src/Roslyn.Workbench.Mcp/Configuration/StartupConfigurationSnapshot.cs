namespace Roslyn.Workbench.Mcp.Configuration;

internal sealed class StartupConfigurationSnapshot
{
    public required StartupOptions Options { get; init; }

    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];
}
