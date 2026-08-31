namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Exposes common metadata and typed visitor dispatch for a materialized plugin tool.
/// </summary>
internal interface IRegisteredPluginTool
{
    /// <summary>
    /// Gets the plugin and transport metadata shared by all handler families.
    /// </summary>
    RegisteredTool Tool { get; }

    /// <summary>
    /// Dispatches the registration to a visitor while retaining its generic handler contract.
    /// </summary>
    /// <typeparam name="TResult">The visitor result type.</typeparam>
    /// <param name="visitor">The registration visitor.</param>
    /// <returns>The visitor result.</returns>
    TResult Accept<TResult>(IPluginToolRegistrationVisitor<TResult> visitor);
}
