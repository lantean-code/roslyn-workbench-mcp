namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Reads Roslyn MEF exports through the non-public compatibility surface used by the current SDK.
/// </summary>
internal interface IMefHostExportProviderCompatibilityAdapter
{
    /// <summary>
    /// Activates and returns all Roslyn MEF exports assignable to the requested type.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="hostServices">The Roslyn host services from which MEF exports are requested.</param>
    /// <returns>The activated exports or a compatibility failure.</returns>
    MefHostExportReadResult<T> ReadExports<T>(MefHostServices hostServices);
}
