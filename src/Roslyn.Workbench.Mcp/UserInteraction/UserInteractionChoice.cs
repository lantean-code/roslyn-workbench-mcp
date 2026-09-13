namespace Roslyn.Workbench.Mcp.UserInteraction;

/// <summary>
/// Represents one stable value and human-readable title offered by an interaction request.
/// </summary>
internal sealed record UserInteractionChoice
{
    /// <summary>
    /// Gets the stable choice value returned to the calling workflow.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the human-readable choice title presented by the client.
    /// </summary>
    public required string Title { get; init; }
}
