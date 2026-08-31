namespace Roslyn.Workbench.Mcp.Plugins.Validation;

/// <summary>
/// Inspects handler types for structural lifetime and composition violations.
/// </summary>
internal interface IPluginHandlerTypeInspector
{
    /// <summary>
    /// Finds errors that prevent a plugin handler type from being materialized safely.
    /// </summary>
    /// <param name="handlerType">The configured handler type.</param>
    /// <returns>All structural error diagnostics.</returns>
    IReadOnlyList<DiagnosticInfo> Inspect(Type handlerType);
}
