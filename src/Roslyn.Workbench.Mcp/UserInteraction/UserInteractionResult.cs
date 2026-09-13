using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.UserInteraction;

/// <summary>
/// Represents one normalised interaction outcome and its protocol-accepted value when available.
/// </summary>
internal sealed record UserInteractionResult
{
    /// <summary>
    /// Gets the normalised interaction outcome.
    /// </summary>
    public UserInteractionOutcome Outcome { get; }

    /// <summary>
    /// Gets the structurally valid choice value returned by the client for workflow validation.
    /// </summary>
    public string? SelectedValue { get; }

    /// <summary>
    /// Gets whether the client accepted the interaction and returned a structurally valid choice value.
    /// </summary>
    [MemberNotNullWhen(true, nameof(SelectedValue))]
    public bool IsAccepted => Outcome == UserInteractionOutcome.Accepted;

    private UserInteractionResult(UserInteractionOutcome outcome, string? selectedValue)
    {
        Outcome = outcome;
        SelectedValue = selectedValue;
    }

    /// <summary>
    /// Creates a result for a protocol-accepted interaction and structurally valid choice value.
    /// </summary>
    /// <param name="selectedValue">The client-returned choice value that the calling workflow must validate.</param>
    /// <returns>An accepted interaction result.</returns>
    public static UserInteractionResult Accepted(string selectedValue)
    {
        return new UserInteractionResult(UserInteractionOutcome.Accepted, selectedValue);
    }

    /// <summary>
    /// Creates a result without a selected value for the supplied outcome.
    /// </summary>
    /// <param name="outcome">The non-accepted interaction outcome.</param>
    /// <returns>A non-accepted interaction result.</returns>
    public static UserInteractionResult NotAccepted(UserInteractionOutcome outcome)
    {
        if (outcome == UserInteractionOutcome.Accepted)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        return new UserInteractionResult(outcome, selectedValue: null);
    }
}
