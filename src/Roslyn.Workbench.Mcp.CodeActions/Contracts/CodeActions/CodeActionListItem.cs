namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents the minimal default descriptor returned when listing code actions.
/// </summary>
public sealed record CodeActionListItem
{
    /// <summary>
    /// Gets the opaque action token.
    /// </summary>
    public string ActionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the stable provider identity.
    /// </summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional action kind.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    /// Gets the execution mode for the discovered action.
    /// </summary>
    public CodeActionExecutionMode? ExecutionMode { get; init; }

    /// <summary>
    /// Gets the dedicated executor tool name when the action is parameterised.
    /// </summary>
    public string? ExecutorTool { get; init; }

    /// <summary>
    /// Gets the descriptor query tool name when the action supports preflight description.
    /// </summary>
    public string? DescribeTool { get; init; }

    /// <summary>
    /// Gets the structured reason code when the action cannot be executed.
    /// </summary>
    public string? UnsupportedReasonCode { get; init; }
}
