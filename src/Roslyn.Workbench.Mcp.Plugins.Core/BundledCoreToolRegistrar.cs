using Roslyn.Workbench.Mcp.Plugins.Core.Inspection;
using Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal static class BundledCoreToolRegistrar
{
    public static void RegisterAll(IPluginRegistry registry)
    {
        GetSolutionStructureTool.Register(registry);
        GetProjectDetailsTool.Register(registry);
        GetDocumentOptionsTool.Register(registry);
        GetDocumentOutlineTool.Register(registry);
        GetCodeMetricsTool.Register(registry);
        GetCodeContextTool.Register(registry);
        SearchSymbolsTool.Register(registry);
        ResolveSymbolTool.Register(registry);
        GetSymbolInfoTool.Register(registry);
        GetSymbolMembersTool.Register(registry);
        GetSymbolAttributesTool.Register(registry);
        GoToDefinitionTool.Register(registry);
        FindReferencesTool.Register(registry);
        FindCallersTool.Register(registry);
        FindCalleesTool.Register(registry);
        FindImplementationsTool.Register(registry);
        FindOverridesTool.Register(registry);
        FindDerivedTypesTool.Register(registry);
        GetTypeHierarchyTool.Register(registry);
        FindOverloadsTool.Register(registry);
        GetPartialDeclarationsTool.Register(registry);
        GetSymbolDependenciesTool.Register(registry);
        GetSymbolDependentsTool.Register(registry);
        GetDependencyGraphTool.Register(registry);
        FindDependencyCyclesTool.Register(registry);
        FindUnusedSymbolsTool.Register(registry);
        FindDuplicateCodeTool.Register(registry);
        GetDiagnosticsTool.Register(registry);
        AnalyzeNullabilityTool.Register(registry);
        AnalyzeAsyncTool.Register(registry);
        AnalyzeDisposablesTool.Register(registry);
        AnalyzeControlFlowTool.Register(registry);
        AnalyzeDataFlowTool.Register(registry);
        GetOperationTreeTool.Register(registry);
        GetControlFlowGraphTool.Register(registry);
        GetChangeImpactTool.Register(registry);
        GetApiSurfaceTool.Register(registry);
        GetTestImpactTool.Register(registry);
        RenameSymbolTool.Register(registry);
        SortUsingsTool.Register(registry);
        FormatDocumentTool.Register(registry);
    }
}
