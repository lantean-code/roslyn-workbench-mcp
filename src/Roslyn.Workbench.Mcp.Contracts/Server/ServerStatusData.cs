using System.Text.Json.Serialization;
using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the structured payload returned by server status.
/// </summary>
public sealed record ServerStatusData
{
    /// <summary>
    /// Gets the server version.
    /// </summary>
    public string ServerVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Roslyn version.
    /// </summary>
    public string RoslynVersion { get; init; } = string.Empty;

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
