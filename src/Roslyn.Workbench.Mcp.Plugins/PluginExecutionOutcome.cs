using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Identifies the outcome of executing a plugin handler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PluginExecutionOutcome>))]
public enum PluginExecutionOutcome
{
    /// <summary>The handler completed successfully.</summary>
    Succeeded,
    /// <summary>The handler completed without producing a change.</summary>
    NoChange,
    /// <summary>The request was rejected.</summary>
    Rejected,
    /// <summary>The request conflicted with current Workspace state.</summary>
    Conflict,
    /// <summary>The handler or execution pipeline faulted.</summary>
    Faulted,
}
