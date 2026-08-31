namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

/// <summary>
/// Defines the supported Code Action resolution failure kind values.
/// </summary>
internal enum CodeActionResolutionFailureKind
{
    /// <summary>
    /// No categorised resolution failure occurred.
    /// </summary>
    None,
    /// <summary>
    /// The provider that originally produced the action is unavailable.
    /// </summary>
    ProviderUnavailable,
    /// <summary>
    /// The temporary reference is missing, expired, stale, or no longer identifies one action.
    /// </summary>
    InvalidReference,
}
