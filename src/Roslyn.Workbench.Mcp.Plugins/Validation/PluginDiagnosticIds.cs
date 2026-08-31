namespace Roslyn.Workbench.Mcp.Plugins.Validation;

/// <summary>
/// Defines stable diagnostic identifiers emitted by plugin discovery, validation and materialization.
/// </summary>
internal static class PluginDiagnosticIds
{
    /// <summary>
    /// The diagnostic identifier for assembly loading failures.
    /// </summary>
    public const string Load = "PluginLoad";

    /// <summary>
    /// The diagnostic identifier for plugin discovery failures.
    /// </summary>
    public const string Discovery = "PluginDiscovery";

    /// <summary>
    /// The diagnostic identifier for invalid plugin identity metadata.
    /// </summary>
    public const string Metadata = "PluginMetadata";

    /// <summary>
    /// The diagnostic identifier for plugin or tool identity collisions.
    /// </summary>
    public const string Collision = "PluginCollision";

    /// <summary>
    /// The diagnostic identifier for handler or service materialization failures.
    /// </summary>
    public const string Materialization = "PluginMaterialization";

    /// <summary>
    /// The diagnostic identifier for plugin dependency-composition failures.
    /// </summary>
    public const string Composition = "PluginComposition";

    /// <summary>
    /// The diagnostic identifier for invalid handler contract combinations.
    /// </summary>
    public const string HandlerContract = "PluginHandlerContract";

    /// <summary>
    /// The diagnostic identifier for handler-owned disposable lifetimes.
    /// </summary>
    public const string HandlerLifetime = "PluginHandlerLifetime";

    /// <summary>
    /// The diagnostic identifier for prohibited MEF composition on handlers.
    /// </summary>
    public const string HandlerComposition = "PluginHandlerComposition";

    /// <summary>
    /// The diagnostic identifier for handler instance state requiring thread-safety review.
    /// </summary>
    public const string HandlerInstanceState = "PluginHandlerInstanceState";

    /// <summary>
    /// The diagnostic identifier for mutable handler properties or events.
    /// </summary>
    public const string HandlerMutableMembers = "PluginHandlerMutableMembers";

    /// <summary>
    /// The diagnostic identifier for mutable static handler state.
    /// </summary>
    public const string HandlerStaticState = "PluginHandlerStaticState";

    /// <summary>
    /// The diagnostic identifier for handler fields that may own disposable resources.
    /// </summary>
    public const string HandlerDisposableField = "PluginHandlerDisposableField";

    /// <summary>
    /// The diagnostic identifier for legacy static registration metadata.
    /// </summary>
    public const string LegacyRegistration = "PluginLegacyRegistration";

    /// <summary>
    /// The diagnostic identifier for missing tool display metadata.
    /// </summary>
    public const string ToolMetadata = "PluginToolMetadata";

    /// <summary>
    /// The diagnostic identifier for tool behaviour incompatible with its handler family.
    /// </summary>
    public const string ToolBehaviour = "PluginToolBehaviour";

    /// <summary>
    /// The diagnostic identifier for invalid or duplicate protocol tool names.
    /// </summary>
    public const string ToolName = "PluginToolName";

    /// <summary>
    /// The diagnostic identifier for request or response schema publication failures.
    /// </summary>
    public const string ToolSchema = "PluginToolSchema";
}
