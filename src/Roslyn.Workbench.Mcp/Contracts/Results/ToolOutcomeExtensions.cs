namespace Roslyn.Workbench.Mcp.Protocol.Results;

/// <summary>
/// Provides helper methods for <see cref="ToolOutcome"/>.
/// </summary>
internal static class ToolOutcomeExtensions
{
    /// <summary>
    /// Gets a value indicating whether the outcome represents an error result.
    /// </summary>
    /// <param name="outcome">The tool outcome to inspect.</param>
    /// <returns><see langword="true"/> when the outcome is rejected, conflicted, or faulted; otherwise, <see langword="false"/>.</returns>
    public static bool IsError(this ToolOutcome outcome)
    {
        return outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted;
    }
}
