namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Identifies the outcome of attempting to activate a diagnostic analyzer.
/// </summary>
internal enum CodeActionAnalyzerActivationStatus
{
    /// <summary>
    /// The analyzer was activated successfully.
    /// </summary>
    Available,
    /// <summary>
    /// The indexed type is not a compatible diagnostic analyzer.
    /// </summary>
    IncompatibleType,
    /// <summary>
    /// Analyzer type metadata could not be inspected.
    /// </summary>
    InspectionFailed,
    /// <summary>
    /// The analyzer instance could not be constructed.
    /// </summary>
    ConstructionFailed,
}
