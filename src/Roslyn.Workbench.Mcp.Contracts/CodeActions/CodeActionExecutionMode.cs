using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.CodeActions;

/// <summary>
/// Describes how a discovered code action can be executed.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CodeActionExecutionMode>))]
public enum CodeActionExecutionMode
{
    /// <summary>
    /// The action can be replayed directly through the generic staging path.
    /// </summary>
    Replay,

    /// <summary>
    /// The action requires a dedicated typed tool and optional preflight description.
    /// </summary>
    Parameterised,

    /// <summary>
    /// The action is discoverable but cannot be executed by the current server.
    /// </summary>
    Unsupported,
}
