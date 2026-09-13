namespace Roslyn.Workbench.Mcp.UserInteraction;

/// <summary>
/// Describes a protocol-neutral titled single-select interaction.
/// </summary>
internal sealed record UserInteractionRequest
{
    /// <summary>
    /// Gets the message explaining the decision being requested.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the title of the choice field.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the description of the available decision.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the stable titled choices offered to the user.
    /// </summary>
    public required IReadOnlyList<UserInteractionChoice> Choices { get; init; }
}
