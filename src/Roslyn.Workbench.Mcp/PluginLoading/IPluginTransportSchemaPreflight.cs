namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Verifies that every prepared plugin tool can publish valid MCP schemas before catalogue publication.
/// </summary>
internal interface IPluginTransportSchemaPreflight
{
    /// <summary>
    /// Validates plugin transport schemas before publication.
    /// </summary>
    /// <param name="tools">The tools whose published transport schemas must be validated.</param>
    /// <returns>The plugin transport schema preflight result.</returns>
    PluginTransportSchemaPreflightResult Preflight(
        IReadOnlyList<PreparedPluginTool> tools);
}
