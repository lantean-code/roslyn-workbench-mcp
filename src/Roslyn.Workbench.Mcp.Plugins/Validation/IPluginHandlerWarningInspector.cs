namespace Roslyn.Workbench.Mcp.Plugins.Validation;

/// <summary>
/// Inspects handler types for state and ownership patterns that require plugin-author review.
/// </summary>
internal interface IPluginHandlerWarningInspector
{
    /// <summary>
    /// Finds advisory diagnostics for a configured handler type and its base types.
    /// </summary>
    /// <param name="handlerType">The configured handler type.</param>
    /// <returns>All state, ownership and legacy-pattern warnings.</returns>
    IReadOnlyList<DiagnosticInfo> Inspect(Type handlerType);
}
