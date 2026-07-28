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
    public Guid ActionId { get; init; }

    /// <summary>
    /// Gets the display title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the action kind.
    /// </summary>
    public CodeActionKind Kind { get; init; }

    /// <summary>
    /// Gets the precise source location to which the action applies.
    /// </summary>
    public required CodeActionLocation Location { get; init; }

    /// <summary>
    /// Gets concise diagnostic context for a code fix.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CodeActionDiagnosticContext>? Diagnostics { get; init; }

    /// <summary>
    /// Gets the supported Fix All scopes for a code fix.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CodeActionFixAllScope>? FixAllScopes { get; init; }
}
