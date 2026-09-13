namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Captures the complete set of host-published Code Action tools fixed at startup.
/// </summary>
internal sealed record CodeActionCatalogSnapshot
{
    /// <summary>
    /// Gets the registered Code Action tools available to the MCP host.
    /// </summary>
    public IReadOnlyList<IRegisteredCodeActionTool> Tools { get; init; } = [];

    /// <summary>
    /// Gets every host-owned Code Action tool name reserved against external plugin publication.
    /// </summary>
    public IReadOnlyList<string> ReservedToolNames { get; init; } = [];
}
