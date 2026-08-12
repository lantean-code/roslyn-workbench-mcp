using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the structured payload returned by server status.
/// </summary>
internal sealed record ServerStatusData
{
    /// <summary>
    /// Gets the server version, when available from assembly metadata.
    /// </summary>
    public string? ServerVersion { get; init; }

    /// <summary>
    /// Gets the Roslyn version, when available from assembly metadata.
    /// </summary>
    public string? RoslynVersion { get; init; }

    /// <summary>
    /// Gets the MSBuild component status.
    /// </summary>
    public ComponentStatus? MsBuild { get; init; }

    /// <summary>
    /// Gets the code-action component status.
    /// </summary>
    public ComponentStatus? CodeActions { get; init; } = new();

    /// <summary>
    /// Gets the effective non-sensitive server configuration.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ServerConfiguration? Configuration { get; init; }

    /// <summary>
    /// Gets the startup configuration fallbacks, when expanded detail is requested.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WarningInfo>? StartupWarnings { get; init; }

    /// <summary>
    /// Gets the loaded tool count.
    /// </summary>
    public int ToolCount { get; init; }

    /// <summary>
    /// Gets the plugin load results.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PluginStatus>? Plugins { get; init; }

    /// <summary>
    /// Gets the unfinished recovery state, when present.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RecoveryStatus>? Recovery { get; init; }
}
