namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Builds the host's catalogue of bundled Code Action tools.
/// </summary>
internal static class BundledCodeActionCatalog
{
    /// <summary>
    /// Creates the bundled Code Action catalog.
    /// </summary>
    /// <returns>The complete set of host-published Code Action tools.</returns>
    public static IReadOnlyList<IRegisteredCodeActionTool> Create()
    {
        var registry = new CodeActionToolRegistry();
        BundledCodeActionToolRegistrar.RegisterAll(registry);
        return registry.Tools.ToArray();
    }
}
