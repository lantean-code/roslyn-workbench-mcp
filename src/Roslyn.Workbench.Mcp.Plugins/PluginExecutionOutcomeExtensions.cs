namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Provides helpers for plugin execution outcomes.
/// </summary>
public static class PluginExecutionOutcomeExtensions
{
    /// <summary>Determines whether an outcome represents an error.</summary>
    /// <param name="outcome">The outcome to inspect.</param>
    /// <returns><see langword="true"/> for rejected, conflicting, or faulted outcomes.</returns>
    public static bool IsError(this PluginExecutionOutcome outcome)
    {
        return outcome is PluginExecutionOutcome.Rejected
            or PluginExecutionOutcome.Conflict
            or PluginExecutionOutcome.Faulted;
    }
}
