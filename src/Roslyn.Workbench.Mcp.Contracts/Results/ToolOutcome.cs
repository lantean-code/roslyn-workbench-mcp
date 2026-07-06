using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Results;

/// <summary>
/// Represents the top-level outcome of a tool invocation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolOutcome>))]
public enum ToolOutcome
{
    /// <summary>
    /// The tool completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The tool completed successfully but did not produce a state change.
    /// </summary>
    NoChange,

    /// <summary>
    /// The request was rejected because the target or server state was invalid.
    /// </summary>
    Rejected,

    /// <summary>
    /// The request conflicted with stale or incompatible workspace state.
    /// </summary>
    Conflict,

    /// <summary>
    /// The tool failed unexpectedly.
    /// </summary>
    Faulted,

    /// <summary>
    /// Composite flag indicating that the tool failed to complete successfully.
    /// </summary>
    IsFailure = Rejected | Conflict | Faulted
}
