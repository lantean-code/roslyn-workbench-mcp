namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Builds the host's catalogue of bundled Code Action tools.
/// </summary>
internal static class BundledCodeActionCatalog
{
    /// <summary>
    /// Gets every host-owned Code Action tool name regardless of the active publication policy.
    /// </summary>
    public static IReadOnlyList<string> ToolNames => BundledCodeActionToolRegistrar.ToolNames;

    /// <summary>
    /// Creates the bundled Code Action catalog.
    /// </summary>
    /// <returns>The complete set of host-published Code Action tools.</returns>
    public static IReadOnlyList<IRegisteredCodeActionTool> Create()
    {
        return Create(includeMutationTools: true);
    }

    /// <summary>
    /// Creates the bundled Code Action catalog with optional mutation tools.
    /// </summary>
    /// <param name="includeMutationTools">Whether mutation tools are created and registered.</param>
    /// <returns>The filtered set of host-published Code Action tools.</returns>
    public static IReadOnlyList<IRegisteredCodeActionTool> Create(bool includeMutationTools)
    {
        var registry = new CodeActionToolRegistry();
        BundledCodeActionToolRegistrar.RegisterAll(registry, includeMutationTools);
        return registry.Tools.ToArray();
    }
}
