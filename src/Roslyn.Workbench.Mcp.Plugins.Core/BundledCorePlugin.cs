using Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;
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

        configuration.Services.AddSingleton<IAnalyzerDiagnosticService, AnalyzerDiagnosticService>();
        configuration.Services.AddSingleton<IBundledAsyncAnalyzerProvider, BundledAsyncAnalyzerProvider>();
        configuration.Services.AddSingleton<IAsyncAnalyzerDiagnosticService, AsyncAnalyzerDiagnosticService>();

        configuration.AddQueryTool<GetSolutionStructureTool>();
        configuration.AddQueryTool<GetProjectDetailsTool>();
        configuration.AddQueryTool<GetDocumentOptionsTool>();
        configuration.AddQueryTool<GetDocumentOutlineTool>();
        configuration.AddQueryTool<GetCodeContextTool>();
        configuration.AddQueryTool<SearchSymbolsTool>();
        configuration.AddQueryTool<ResolveSymbolTool>();
        configuration.AddQueryTool<GetSymbolInfoTool>();
        configuration.AddQueryTool<GetSymbolMembersTool>();
        configuration.AddQueryTool<GetSymbolAttributesTool>();
        configuration.AddQueryTool<GoToDefinitionTool>();
        configuration.AddQueryTool<FindReferencesTool>();
        configuration.AddQueryTool<FindCallersTool>();
        configuration.AddQueryTool<FindCalleesTool>();
        configuration.AddQueryTool<FindImplementationsTool>();
        configuration.AddQueryTool<FindOverridesTool>();
        configuration.AddQueryTool<FindDerivedTypesTool>();
        configuration.AddQueryTool<GetTypeHierarchyTool>();
        configuration.AddQueryTool<FindOverloadsTool>();
        configuration.AddQueryTool<GetPartialDeclarationsTool>();
        configuration.AddQueryTool<GetSymbolDependenciesTool>();
        configuration.AddQueryTool<GetSymbolDependentsTool>();
        configuration.AddQueryTool<GetDependencyGraphTool>();
        configuration.AddQueryTool<FindDependencyCyclesTool>();
        configuration.AddQueryTool<FindUnusedSymbolsTool>();
        configuration.AddQueryTool<FindDuplicateCodeTool>();
        configuration.AddQueryTool<GetDiagnosticsTool>();
        configuration.AddQueryTool<AnalyzeNullabilityTool>();
        configuration.AddQueryTool<AnalyzeAsyncTool>();
        configuration.AddQueryTool<AnalyzeDisposablesTool>();
        configuration.AddQueryTool<AnalyzeControlFlowTool>();
        configuration.AddQueryTool<AnalyzeDataFlowTool>();
        configuration.AddQueryTool<GetOperationTreeTool>();
        configuration.AddQueryTool<GetControlFlowGraphTool>();
        configuration.AddQueryTool<GetChangeImpactTool>();
        configuration.AddQueryTool<GetApiSurfaceTool>();
        configuration.AddQueryTool<GetTestImpactTool>();
        configuration.AddMutationTool<RenameSymbolTool>();
        configuration.AddMutationTool<FormatDocumentTool>();
    }
}
