
namespace Roslyn.Workbench.Mcp;

internal sealed class StartupOptions
{
    public IReadOnlyList<string> PluginDirectories { get; set; } = [];

    public int DefaultMaxResults { get; set; } = 100;

    public TimeSpan CodeActionTokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

    public int MaxTransactionRevisions { get; set; } = 20;

    public int MaxConcurrentQueries { get; set; } = 2;

    public ToolOutputSchemaMode ToolOutputSchemaMode { get; set; } = ToolOutputSchemaMode.Omit;

    public string StateDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-state");
}
