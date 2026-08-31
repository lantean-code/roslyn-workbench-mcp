namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Validates the identity and target framework declared by a plugin entry assembly.
/// </summary>
internal interface IPluginEntryPointValidator
{
    /// <summary>
    /// Gets the reason an entry point is incompatible with the host.
    /// </summary>
    /// <param name="entryPoint">The entry-point assembly used to discover application dependencies.</param>
    /// <returns>A validation message when the entry point is incompatible; otherwise, <see langword="null"/>.</returns>
    string? GetValidationError(PluginEntryPointMetadata entryPoint);
}
