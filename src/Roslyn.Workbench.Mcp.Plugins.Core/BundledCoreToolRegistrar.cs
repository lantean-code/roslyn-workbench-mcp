using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal static class BundledCoreToolRegistrar
{
    public static void RegisterAll(IPluginRegistry registry)
    {
        GetSolutionStructureTool.Register(registry);
        GetProjectDetailsTool.Register(registry);
        GetDocumentOptionsTool.Register(registry);
        GetDocumentOutlineTool.Register(registry);
        SearchSymbolsTool.Register(registry);
        ResolveSymbolTool.Register(registry);
        GetSymbolInfoTool.Register(registry);
        GetSymbolMembersTool.Register(registry);
        GetSymbolAttributesTool.Register(registry);
        GoToDefinitionTool.Register(registry);
        FindReferencesTool.Register(registry);
        FindCallersTool.Register(registry);
        FindImplementationsTool.Register(registry);
        FindDerivedTypesTool.Register(registry);
        GetTypeHierarchyTool.Register(registry);
        FindOverloadsTool.Register(registry);
        GetPartialDeclarationsTool.Register(registry);
        GetDiagnosticsTool.Register(registry);
        AnalyzeControlFlowTool.Register(registry);
        AnalyzeDataFlowTool.Register(registry);
        GetOperationTreeTool.Register(registry);
        GetControlFlowGraphTool.Register(registry);
        RenameSymbolTool.Register(registry);
        SortUsingsTool.Register(registry);
        FormatDocumentTool.Register(registry);
        RemoveUnusedUsingsTool.Register(registry);
        ListCodeActionsTool.Register(registry);
        StageCodeActionTool.Register(registry);
        StageCodeFixTool.Register(registry);
        StageFixAllTool.Register(registry);
    }
}
