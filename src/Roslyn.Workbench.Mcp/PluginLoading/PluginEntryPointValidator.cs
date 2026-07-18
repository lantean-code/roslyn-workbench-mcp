using NuGet.Versioning;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginEntryPointValidator : IPluginEntryPointValidator
{
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
