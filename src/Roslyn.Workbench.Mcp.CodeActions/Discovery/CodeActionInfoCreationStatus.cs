namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Identifies why a discovered Code Action could or could not be published.
/// </summary>
internal enum CodeActionInfoCreationStatus
{
    /// <summary>
    /// The action item and its reference were created.
    /// </summary>
    Succeeded,
    /// <summary>
    /// The action did not have a usable source location.
    /// </summary>
    LocationUnavailable,
    /// <summary>
    /// The action's document did not have a publishable path.
    /// </summary>
    DocumentPathUnavailable,
    /// <summary>
    /// The short-lived reference store had no remaining capacity.
    /// </summary>
    ReferenceCapacityExceeded,
}
