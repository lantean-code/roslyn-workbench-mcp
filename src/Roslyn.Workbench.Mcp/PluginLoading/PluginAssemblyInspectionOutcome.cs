namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Identifies whether assembly metadata contained usable plugin entry points.
/// </summary>
internal enum PluginAssemblyInspectionOutcome
{
    /// <summary>
    /// One or more valid plugin entry points were discovered.
    /// </summary>
    Success,
    /// <summary>
    /// The candidate is not a managed plugin assembly and should be ignored.
    /// </summary>
    Skipped,
    /// <summary>
    /// The candidate appears relevant but its metadata is invalid or unreadable.
    /// </summary>
    Failure,
}
