namespace Roslyn.Workbench.Mcp.UserInteraction;

/// <summary>
/// Identifies the normalised outcome of a user interaction request.
/// </summary>
internal enum UserInteractionOutcome
{
    /// <summary>
    /// The client accepted the interaction and returned a structurally valid choice value for workflow validation.
    /// </summary>
    Accepted = 0,

    /// <summary>
    /// The client declined the interaction.
    /// </summary>
    Declined = 1,

    /// <summary>
    /// The client cancelled the interaction.
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// The client cannot perform the required interaction.
    /// </summary>
    Unavailable = 3,

    /// <summary>
    /// The client failed while performing the interaction.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// The client returned an invalid or unsupported response.
    /// </summary>
    InvalidResponse = 5,
}
