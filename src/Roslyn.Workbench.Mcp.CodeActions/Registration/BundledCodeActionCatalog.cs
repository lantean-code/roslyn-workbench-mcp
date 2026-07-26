namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

internal static class BundledCodeActionCatalog
{
    public static IReadOnlyList<IRegisteredCodeActionTool> Create()
    {
        var registry = new CodeActionToolRegistry();
        BundledCodeActionToolRegistrar.RegisterAll(registry);
        return registry.Tools.ToArray();
    }
}
