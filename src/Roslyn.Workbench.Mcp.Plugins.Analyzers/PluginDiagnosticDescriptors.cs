using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Defines the stable diagnostic metadata published by the plugin authoring analyzers.
/// </summary>
internal static class PluginDiagnosticDescriptors
{
    private const string _category = "RoslynWorkbench.PluginAuthoring";
    private const string _helpLinkBase = "https://github.com/lantean-code/roslyn-workbench-mcp/blob/main/docs/PluginAuthoringDiagnostics.md";

    /// <summary>
    /// Describes RWMCP001, reported when plugin code attempts to mutate a Roslyn workspace directly.
    /// </summary>
    public static readonly DiagnosticDescriptor DirectWorkspaceMutation = Create(
        "RWMCP001",
        "Do not mutate the Roslyn Workspace directly",
        "Do not call Workspace.TryApplyChanges; return a MutationCandidate through a mutation tool",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP002, reported when plugin code reads the live workspace solution instead of its invocation snapshot.
    /// </summary>
    public static readonly DiagnosticDescriptor LiveWorkspaceSolution = Create(
        "RWMCP002",
        "Use the invocation solution snapshot",
        "Do not read Workspace.CurrentSolution; use the invocation context's CurrentSolution snapshot",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP003, reported when plugin startup configuration is asynchronous.
    /// </summary>
    public static readonly DiagnosticDescriptor AsynchronousPluginConfiguration = Create(
        "RWMCP003",
        "Plugin configuration must complete synchronously",
        "IRoslynPlugin.Configure must not be async",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP004, reported when plugin startup configuration state escapes its configuration callback.
    /// </summary>
    public static readonly DiagnosticDescriptor RetainedPluginConfiguration = Create(
        "RWMCP004",
        "Do not retain startup configuration objects",
        "Do not retain or escape the startup configuration object or a tool configuration builder",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP005, reported when a handler does not implement exactly one query or mutation contract.
    /// </summary>
    public static readonly DiagnosticDescriptor HandlerContract = Create(
        "RWMCP005",
        "Implement exactly one handler contract",
        "Plugin handler '{0}' must implement exactly one closed query or mutation handler contract and no contract from the other family",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP006, reported when a handler claims a disposable lifetime.
    /// </summary>
    public static readonly DiagnosticDescriptor DisposableHandler = Create(
        "RWMCP006",
        "Plugin handlers must not own a disposable lifetime",
        "Plugin handler '{0}' must not implement IDisposable or IAsyncDisposable",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP007, reported when a handler declares a MEF import.
    /// </summary>
    public static readonly DiagnosticDescriptor HandlerMefImport = Create(
        "RWMCP007",
        "Plugin handlers must not declare MEF imports",
        "Plugin handler member '{0}' must not declare a MEF import",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP008, reported when an externally serialized tool contract is not fully public.
    /// </summary>
    public static readonly DiagnosticDescriptor PublicTransportContract = Create(
        "RWMCP008",
        "External transport contract types must be public",
        "Tool contract type '{0}' and all containing and component types must be public",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP009, reported when handler instance state requires an explicit thread-safety review.
    /// </summary>
    public static readonly DiagnosticDescriptor HandlerInstanceState = Create(
        "RWMCP009",
        "Handler instance state requires thread-safety review",
        "Plugin handler member '{0}' introduces instance state and requires a thread-safety review",
        DiagnosticSeverity.Warning);

    /// <summary>
    /// Describes RWMCP010, reported when a handler declares mutable static state.
    /// </summary>
    public static readonly DiagnosticDescriptor MutableStaticHandlerState = Create(
        "RWMCP010",
        "Avoid mutable static handler state",
        "Plugin handler field '{0}' declares mutable static state",
        DiagnosticSeverity.Warning);

    /// <summary>
    /// Describes RWMCP011, reported when a handler field may retain a disposable resource.
    /// </summary>
    public static readonly DiagnosticDescriptor DisposableHandlerField = Create(
        "RWMCP011",
        "Handler field may own a disposable resource",
        "Plugin handler field '{0}' may own a disposable resource",
        DiagnosticSeverity.Warning);

    /// <summary>
    /// Describes RWMCP012, reported when a query handler declares destructive behaviour.
    /// </summary>
    public static readonly DiagnosticDescriptor DestructiveQueryHandler = Create(
        "RWMCP012",
        "Query tools cannot declare destructive behaviour",
        "Query handler '{0}' cannot declare destructive behaviour",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP013, reported when a handler does not meaningfully observe or forward invocation cancellation.
    /// </summary>
    public static readonly DiagnosticDescriptor UnobservedCancellationToken = Create(
        "RWMCP013",
        "Observe the invocation cancellation token",
        "Handler method '{0}' does not meaningfully observe or forward its cancellation token",
        DiagnosticSeverity.Info);

    /// <summary>
    /// Describes RWMCP014, reported when an agent-facing query response exposes an unbounded collection.
    /// </summary>
    public static readonly DiagnosticDescriptor UnboundedQueryCollection = Create(
        "RWMCP014",
        "Bound agent-facing query collections",
        "Query response member '{0}' exposes an unbounded collection; use BoundedCollection<TItem> within a dedicated IQueryResponse DTO",
        DiagnosticSeverity.Warning);

    /// <summary>
    /// Describes RWMCP015, reported when a plugin marker and entry-point contract do not agree.
    /// </summary>
    public static readonly DiagnosticDescriptor PluginEntryPointContract = Create(
        "RWMCP015",
        "Plugin entry-point marker and contract must agree",
        "Plugin entry-point type '{0}' must be a concrete IRoslynPlugin implementation with RoslynPluginAttribute",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP016, reported when an assembly declares more than one marked plugin entry point.
    /// </summary>
    public static readonly DiagnosticDescriptor MultiplePluginEntryPoints = Create(
        "RWMCP016",
        "A plugin assembly cannot declare multiple marked entry points",
        "Plugin entry point '{0}' conflicts with another RoslynPluginAttribute in this assembly",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP017, reported when a plugin declares an API version other than the referenced supported version.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedPluginApiVersion = Create(
        "RWMCP017",
        "Declare the supported plugin API version",
        "Plugin API version must be the referenced Plugins API version '{0}'",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP018, reported when required plugin identity metadata is blank.
    /// </summary>
    public static readonly DiagnosticDescriptor BlankPluginIdentity = Create(
        "RWMCP018",
        "Plugin identity metadata must not be blank",
        "Plugin {0} must not be null, empty or whitespace",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP019, reported when tool metadata decorates a type without a closed handler contract.
    /// </summary>
    public static readonly DiagnosticDescriptor ToolMetadataWithoutHandler = Create(
        "RWMCP019",
        "Tool metadata must decorate a handler",
        "Type '{0}' declares RoslynToolAttribute but implements no closed query or mutation handler contract",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP020, reported when a query-cache key is not immutable and structurally stable.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidQueryCacheKey = Create(
        "RWMCP020",
        "Use a dedicated immutable query-cache key",
        "Query-cache key type '{0}' must be a sealed immutable reference type with stable value equality and structurally safe members",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP021, reported when a cached value may be mutable, disposable or otherwise unsafe to retain.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsafeCachedValue = Create(
        "RWMCP021",
        "Cached value may be unsafe to retain",
        "Query-cache value type '{0}' is mutable, disposable or a result envelope and should not be retained without a specific justification",
        DiagnosticSeverity.Warning);

    /// <summary>
    /// Describes RWMCP022, reported when a tool name is incompatible with the MCP naming policy.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidToolName = Create(
        "RWMCP022",
        "Use a protocol-compatible MCP tool name",
        "Tool name '{0}' must contain 1 to 128 ASCII letters, digits, underscores, hyphens, or periods",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Describes RWMCP023, reported when plugin code throws a transport-owned MCP protocol exception.
    /// </summary>
    public static readonly DiagnosticDescriptor PluginProtocolException = Create(
        "RWMCP023",
        "Do not throw MCP protocol exceptions from plugins",
        "Plugin code must not throw McpProtocolException; return a plugin execution failure instead",
        DiagnosticSeverity.Error);

    private static DiagnosticDescriptor Create(
        string id,
        LocalizableString title,
        LocalizableString messageFormat,
        DiagnosticSeverity severity)
    {
        var helpLink = $"{_helpLinkBase}#{id}";
        var descriptor = new DiagnosticDescriptor(
            id,
            title,
            messageFormat,
            _category,
            severity,
            isEnabledByDefault: true,
            helpLinkUri: helpLink);

        return descriptor;
    }
}
