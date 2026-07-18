using Roslyn.Workbench.Mcp.Plugins.Core.Inspection;
using Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

/// <summary>
/// Registers the bundled first-party plugin assembly.
/// </summary>
[RoslynPlugin("roslyn.workbench.core", "Roslyn Workbench Core", PluginApiVersions.V1)]
public sealed class BundledCorePlugin : IRoslynPlugin
{
    /// <summary>
    /// Configures bundled first-party tools.
    /// </summary>
    /// <param name="configuration">The plugin configuration.</param>
    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _ = configuration.AddQueryTool<GetSolutionStructureTool>();
        _ = configuration.AddQueryTool<GetProjectDetailsTool>();
        _ = configuration.AddQueryTool<GetDocumentOptionsTool>();
        _ = configuration.AddQueryTool<GetDocumentOutlineTool>();
        _ = configuration.AddQueryTool<GetCodeMetricsTool>();
        _ = configuration.AddQueryTool<GetCodeContextTool>();
        _ = configuration.AddQueryTool<SearchSymbolsTool>();
        _ = configuration.AddQueryTool<ResolveSymbolTool>();
        _ = configuration.AddQueryTool<GetSymbolInfoTool>();
        _ = configuration.AddQueryTool<GetSymbolMembersTool>();
        _ = configuration.AddQueryTool<GetSymbolAttributesTool>();
        _ = configuration.AddQueryTool<GoToDefinitionTool>();
        _ = configuration.AddQueryTool<FindReferencesTool>();
        _ = configuration.AddQueryTool<FindCallersTool>();
        _ = configuration.AddQueryTool<FindCalleesTool>();
        _ = configuration.AddQueryTool<FindImplementationsTool>();
        _ = configuration.AddQueryTool<FindOverridesTool>();
        _ = configuration.AddQueryTool<FindDerivedTypesTool>();
        _ = configuration.AddQueryTool<GetTypeHierarchyTool>();
        _ = configuration.AddQueryTool<FindOverloadsTool>();
        _ = configuration.AddQueryTool<GetPartialDeclarationsTool>();
        _ = configuration.AddQueryTool<GetSymbolDependenciesTool>();
        _ = configuration.AddQueryTool<GetSymbolDependentsTool>();
        _ = configuration.AddQueryTool<GetDependencyGraphTool>();
        _ = configuration.AddQueryTool<FindDependencyCyclesTool>();
        _ = configuration.AddQueryTool<FindUnusedSymbolsTool>();
        _ = configuration.AddQueryTool<FindDuplicateCodeTool>();
        _ = configuration.AddQueryTool<GetDiagnosticsTool>();
        _ = configuration.AddQueryTool<AnalyzeNullabilityTool>();
        _ = configuration.AddQueryTool<AnalyzeAsyncTool>();
        _ = configuration.AddQueryTool<AnalyzeDisposablesTool>();
        _ = configuration.AddQueryTool<AnalyzeControlFlowTool>();
        _ = configuration.AddQueryTool<AnalyzeDataFlowTool>();
        _ = configuration.AddQueryTool<GetOperationTreeTool>();
        _ = configuration.AddQueryTool<GetControlFlowGraphTool>();
        _ = configuration.AddQueryTool<GetChangeImpactTool>();
        _ = configuration.AddQueryTool<GetApiSurfaceTool>();
        _ = configuration.AddQueryTool<GetTestImpactTool>();
        _ = configuration.AddMutationTool<RenameSymbolTool>();
        _ = configuration.AddMutationTool<SortUsingsTool>();
        _ = configuration.AddMutationTool<FormatDocumentTool>();
    }
}
