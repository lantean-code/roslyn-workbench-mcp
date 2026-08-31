using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to list applicable code actions for a document.
/// </summary>
internal sealed record ListCodeActionsRequest : WorkspaceBoundRequest
{
    private const int _defaultLimit = 50;

    /// <summary>
    /// Target document.
    /// </summary>
    [Description("Target document.")]
    public required DocumentSelector Document { get; init; }

    /// <summary>
    /// The optional selection or caret range. An omitted range selects the complete document.
    /// </summary>
    [Description("The optional selection or caret range. An omitted range selects the complete document.")]
    public TextSpanRange? Range { get; init; }

    /// <summary>
    /// Gets the expected workspace snapshot against which the document and range were resolved.
    /// </summary>
    public required SnapshotPrecondition ExpectedSnapshot { get; init; }

    /// <summary>
    /// The kinds of actions to discover.
    /// </summary>
    [Description("The kinds of actions to discover.")]
    public required CodeActionKindSelection Kinds { get; init; }

    /// <summary>
    /// The optional diagnostic identifier filter for code fixes.
    /// </summary>
    [Description("The optional diagnostic identifier filter for code fixes.")]
    public IReadOnlyList<string>? DiagnosticIds { get; init; }

    /// <summary>
    /// The maximum number of action leaves to return.
    /// </summary>
    [Description("The maximum number of action leaves to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultLimit)]
    public int? Limit { get; init; } = _defaultLimit;

    /// <summary>
    /// Gets the requested action limit or its default when omitted.
    /// </summary>
    internal int EffectiveLimit => ResultLimit.GetEffectiveValue(Limit, _defaultLimit);
}
