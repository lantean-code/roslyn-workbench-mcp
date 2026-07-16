namespace Roslyn.Workbench.Mcp.CodeActions;

internal static class BundledCodeActionCatalog
{
    public static IReadOnlyList<IRegisteredCodeActionTool> Create()
    {
        var registry = new CodeActionToolRegistry();
        BundledCodeActionToolRegistrar.RegisterAll(registry);
        return registry.Tools
            .Where(tool =>
                !BuiltInCodeActionLedger.IsDedicatedTool(tool.Metadata.Name)
                || BuiltInCodeActionLedger.IsDedicatedToolVisible(tool.Metadata.Name))
            .ToArray();
    }
}
