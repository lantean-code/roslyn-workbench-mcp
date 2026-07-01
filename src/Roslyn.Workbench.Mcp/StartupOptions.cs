using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp;

internal sealed class StartupOptions
{
    public IReadOnlyList<string> PluginDirectories { get; init; } = [];

    public int DefaultMaxResults { get; init; } = 100;

    public int MaxResponseBytes { get; init; } = 4 * 1024 * 1024;

    public TimeSpan CodeActionTokenLifetime { get; init; } = TimeSpan.FromMinutes(5);

    public int MaxTransactionRevisions { get; init; } = 20;

    public int MaxConcurrentQueries { get; init; } = 2;

    public ToolOutputSchemaMode ToolOutputSchemaMode { get; init; } = ToolOutputSchemaMode.Omit;

    public string StateDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-state");
}
