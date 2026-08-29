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
    [Description("The server version, when available from assembly metadata.")]
    public string? ServerVersion { get; init; }

    /// <summary>
    /// Gets the Roslyn version, when available from assembly metadata.
    /// </summary>
    [Description("The Roslyn version, when available from assembly metadata.")]
    public string? RoslynVersion { get; init; }

    /// <summary>
    /// Gets the MSBuild component status.
    /// </summary>
    [Description("The MSBuild component status.")]
    public ComponentStatus? MsBuild { get; init; }

    /// <summary>
    /// Gets the code-action component status.
    /// </summary>
    [Description("The code-action component status.")]
    public ComponentStatus? CodeActions { get; init; } = new();

    /// <summary>
    /// Gets the effective non-sensitive server configuration.
    /// </summary>
    [Description("The effective non-sensitive server configuration.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ServerConfiguration? Configuration { get; init; }

    /// <summary>
    /// Gets the startup configuration fallbacks, when expanded detail is requested.
    /// </summary>
    [Description("The startup configuration fallbacks, when expanded detail is requested.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WarningInfo>? StartupWarnings { get; init; }

    /// <summary>
    /// Gets the loaded tool count.
    /// </summary>
    [Description("The loaded tool count.")]
    public int ToolCount { get; init; }

    /// <summary>
    /// Gets the plugin load results.
    /// </summary>
    [Description("The plugin load results.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PluginStatus>? Plugins { get; init; }

    /// <summary>
    /// Gets the unfinished recovery state, when present.
    /// </summary>
    [Description("The unfinished recovery state, when present.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RecoveryStatus>? Recovery { get; init; }
}
