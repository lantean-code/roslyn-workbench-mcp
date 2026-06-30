using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.CodeActions;

/// <summary>
/// Describes the dynamic context published for a discovered code action.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CodeActionDescriptorContextKind>))]
public enum CodeActionDescriptorContextKind
{
    /// <summary>
    /// No additional context is required.
    /// </summary>
    None,

    /// <summary>
    /// The action requires a name and simple option values.
    /// </summary>
    NameOnly,

    /// <summary>
    /// The action requires member selection.
    /// </summary>
    MemberSelection,

    /// <summary>
    /// The action requires a signature change plan.
    /// </summary>
    SignaturePlan,

    /// <summary>
    /// The action requires interface implementation choices.
    /// </summary>
    InterfaceImplementation,

    /// <summary>
    /// The action is not executable by the current server.
    /// </summary>
    Unsupported,
}
