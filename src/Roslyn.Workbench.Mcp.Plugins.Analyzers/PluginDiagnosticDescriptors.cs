using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

internal static class PluginDiagnosticDescriptors
{
    private const string _category = "RoslynWorkbench.PluginAuthoring";
    private const string _helpLinkBase = "https://github.com/lantean-code/roslyn-workbench-mcp/blob/main/docs/PluginAuthoringDiagnostics.md";

    public static readonly DiagnosticDescriptor DirectWorkspaceMutation = Create(
        "RWMCP001",
        "Do not mutate the Roslyn Workspace directly",
        "Do not call Workspace.TryApplyChanges; return a MutationCandidate through a mutation tool",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor LiveWorkspaceSolution = Create(
        "RWMCP002",
        "Use the invocation solution snapshot",
        "Do not read Workspace.CurrentSolution; use the invocation context's CurrentSolution snapshot",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor AsynchronousPluginConfiguration = Create(
        "RWMCP003",
        "Plugin configuration must complete synchronously",
        "IRoslynPlugin.Configure must not be async",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor RetainedPluginConfiguration = Create(
        "RWMCP004",
        "Do not retain startup configuration objects",
        "Do not retain or escape the startup configuration object or a tool configuration builder",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor HandlerContract = Create(
        "RWMCP005",
        "Implement exactly one handler contract",
        "Plugin handler '{0}' must implement exactly one closed query or mutation handler contract and no contract from the other family",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor DisposableHandler = Create(
        "RWMCP006",
        "Plugin handlers must not own a disposable lifetime",
        "Plugin handler '{0}' must not implement IDisposable or IAsyncDisposable",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor HandlerMefImport = Create(
        "RWMCP007",
        "Plugin handlers must not declare MEF imports",
        "Plugin handler member '{0}' must not declare a MEF import",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor PublicTransportContract = Create(
        "RWMCP008",
        "External transport contract types must be public",
        "Tool contract type '{0}' and all containing and component types must be public",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor HandlerInstanceState = Create(
        "RWMCP009",
        "Handler instance state requires thread-safety review",
        "Plugin handler member '{0}' introduces instance state and requires a thread-safety review",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor MutableStaticHandlerState = Create(
        "RWMCP010",
        "Avoid mutable static handler state",
        "Plugin handler field '{0}' declares mutable static state",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor DisposableHandlerField = Create(
        "RWMCP011",
        "Handler field may own a disposable resource",
        "Plugin handler field '{0}' may own a disposable resource",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor DestructiveQueryHandler = Create(
        "RWMCP012",
        "Query tools cannot declare destructive behaviour",
        "Query handler '{0}' cannot declare destructive behaviour",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor UnobservedCancellationToken = Create(
        "RWMCP013",
        "Observe the invocation cancellation token",
        "Handler method '{0}' does not meaningfully observe or forward its cancellation token",
        DiagnosticSeverity.Info);

    public static readonly DiagnosticDescriptor UnboundedQueryCollection = Create(
        "RWMCP014",
        "Bound agent-facing query collections",
        "Query response member '{0}' exposes an unbounded collection; use BoundedCollection<TItem>",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor PluginEntryPointContract = Create(
        "RWMCP015",
        "Plugin entry-point marker and contract must agree",
        "Plugin entry-point type '{0}' must be a concrete IRoslynPlugin implementation with RoslynPluginAttribute",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor MultiplePluginEntryPoints = Create(
        "RWMCP016",
        "A plugin assembly cannot declare multiple marked entry points",
        "Plugin entry point '{0}' conflicts with another RoslynPluginAttribute in this assembly",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor UnsupportedPluginApiVersion = Create(
        "RWMCP017",
        "Declare the supported plugin API version",
        "Plugin API version must be the referenced Plugins API version '{0}'",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor BlankPluginIdentity = Create(
        "RWMCP018",
        "Plugin identity metadata must not be blank",
        "Plugin {0} must not be null, empty or whitespace",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor ToolMetadataWithoutHandler = Create(
        "RWMCP019",
        "Tool metadata must decorate a handler",
        "Type '{0}' declares RoslynToolAttribute but implements no closed query or mutation handler contract",
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
