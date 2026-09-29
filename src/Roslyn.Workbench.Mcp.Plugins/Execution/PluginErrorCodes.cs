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
    /// More than one addressable document matches the selector.
    /// </summary>
    public const string DocumentAmbiguous = "DocumentAmbiguous";

    /// <summary>
    /// No addressable document matches the selector.
    /// </summary>
    public const string DocumentNotFound = "DocumentNotFound";

    /// <summary>
    /// The document selector is invalid.
    /// </summary>
    public const string DocumentSelectorInvalid = "DocumentSelectorInvalid";

    /// <summary>
    /// The supplied tool request is invalid.
    /// </summary>
    public const string InvalidRequest = "InvalidRequest";

    /// <summary>
    /// The requested source location could not be resolved.
    /// </summary>
    public const string LocationNotFound = "LocationNotFound";

    /// <summary>
    /// More than one source location matches the selector.
    /// </summary>
    public const string LocationAmbiguous = "LocationAmbiguous";

    /// <summary>
    /// The source-location selector is invalid.
    /// </summary>
    public const string LocationSelectorInvalid = "LocationSelectorInvalid";

    /// <summary>
    /// More than one project matches the selector.
    /// </summary>
    public const string ProjectAmbiguous = "ProjectAmbiguous";

    /// <summary>
    /// No project matches the selector.
    /// </summary>
    public const string ProjectNotFound = "ProjectNotFound";

    /// <summary>
    /// The project selector is invalid.
    /// </summary>
    public const string ProjectSelectorInvalid = "ProjectSelectorInvalid";

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
    /// More than one symbol matches the selector.
    /// </summary>
    public const string SymbolAmbiguous = "SymbolAmbiguous";

    /// <summary>
    /// The symbol selector is invalid.
    /// </summary>
    public const string SymbolSelectorInvalid = "SymbolSelectorInvalid";

    /// <summary>
    /// A tool failed with an unexpected exception.
    /// </summary>
    public const string UnhandledException = "UnhandledException";
}
