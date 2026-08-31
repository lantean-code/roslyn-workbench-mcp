using NuGet.Versioning;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Validates required plugin identity, semantic version and host API compatibility.
/// </summary>
internal sealed class PluginEntryPointValidator : IPluginEntryPointValidator
{
    /// <summary>
    /// Gets the reason an entry point is incompatible with the host.
    /// </summary>
    /// <param name="entryPoint">The entry-point assembly used to discover application dependencies.</param>
    /// <returns>A validation message when the entry point is incompatible; otherwise, <see langword="null"/>.</returns>
    public string? GetValidationError(PluginEntryPointMetadata entryPoint)
    {
        if (string.IsNullOrWhiteSpace(entryPoint.PluginId)
            || string.IsNullOrWhiteSpace(entryPoint.DisplayName)
            || string.IsNullOrWhiteSpace(entryPoint.SupportedApiVersion))
        {
            return "Plugin metadata must provide a plugin ID, display name, and supported API version.";
        }

        if (!NuGetVersion.TryParse(entryPoint.Version, out _))
        {
            return $"Plugin '{entryPoint.PluginId}' has invalid AssemblyInformationalVersion '{entryPoint.Version}'.";
        }

        return string.Equals(entryPoint.SupportedApiVersion, PluginApiVersions.V1, StringComparison.Ordinal)
            ? null
            : $"Plugin '{entryPoint.PluginId}' declares unsupported API version '{entryPoint.SupportedApiVersion}'.";
    }
}
