namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Defines stable error codes emitted by plugin adapters and bundled plugin handlers.
/// </summary>
internal static class PluginErrorCodes
{
    /// <summary>
    /// Analysis stopped because a configured safety limit was reached.
    /// </summary>
    public const string AnalysisLimitExceeded = "AnalysisLimitExceeded";

    /// <summary>
    /// The supplied tool request is invalid.
    /// </summary>
    public const string InvalidRequest = "InvalidRequest";

    /// <summary>
    /// The requested source location could not be resolved.
    /// </summary>
    public const string LocationNotFound = "LocationNotFound";

    /// <summary>
    /// The requested project structure could not be produced.
    /// </summary>
    public const string ProjectStructureUnavailable = "ProjectStructureUnavailable";

    /// <summary>
    /// The supplied snapshot no longer identifies the current Workspace state.
    /// </summary>
    public const string SnapshotMismatch = "SnapshotMismatch";

    /// <summary>
    /// The requested symbol could not be resolved.
    /// </summary>
    public const string SymbolNotFound = "SymbolNotFound";

    /// <summary>
    /// A tool failed with an unexpected exception.
    /// </summary>
    public const string UnhandledException = "UnhandledException";
}
