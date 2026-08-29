using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents one discovered code action.
/// </summary>
internal sealed record CodeActionListItem
{
    /// <summary>
    /// Gets the opaque action reference.
    /// </summary>
    [Description("The opaque action reference.")]
    public required Guid ActionId { get; init; }

    /// <summary>
    /// Gets the display title.
    /// </summary>
    [Description("The display title.")]
    public required string Title { get; init; }

    /// <summary>
    /// Gets the action kind.
    /// </summary>
    [Description("The action kind.")]
    public required CodeActionKind Kind { get; init; }

    /// <summary>
    /// Gets the precise source location to which the action applies.
    /// </summary>
    [Description("The precise source location to which the action applies.")]
    public required CodeActionLocation Location { get; init; }

    /// <summary>
    /// Gets concise diagnostic context for a code fix.
    /// </summary>
    [Description("Concise diagnostic context for a code fix.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BoundedCollection<CodeActionDiagnosticContext>? Diagnostics { get; init; }

    /// <summary>
    /// Gets the supported Fix All scopes for a code fix.
    /// </summary>
    [Description("The supported Fix All scopes for a code fix.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CodeActionFixAllScope>? FixAllScopes { get; init; }
}
