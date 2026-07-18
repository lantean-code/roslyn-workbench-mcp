namespace Roslyn.Workbench.Mcp.PluginLoading;

internal static class PluginCatalogStatusFactory
{
    public static PluginStatus CreateEnabled(PluginMetadata metadata, IReadOnlyList<DiagnosticInfo> diagnostics)
    {
        return new PluginStatus
        {
            PluginId = metadata.PluginId,
            DisplayName = metadata.DisplayName,
            Version = metadata.Version,
            SupportedApiVersion = metadata.SupportedApiVersion,
            Enabled = true,
            Diagnostics = diagnostics,
        };
    }

    public static PluginStatus CreateDisabled(PluginEntryPointMetadata metadata, string message)
    {
        return CreateDisabled(metadata.PluginId, metadata.DisplayName, metadata.Version, metadata.SupportedApiVersion, message);
    }

    public static PluginStatus CreateDisabled(PluginEntryPointMetadata metadata, string diagnosticId, string message)
    {
        return CreateDisabled(
            metadata.PluginId,
            metadata.DisplayName,
            metadata.Version,
            metadata.SupportedApiVersion,
            diagnosticId,
            message);
    }

    public static PluginStatus CreateDisabled(PluginMetadata metadata, string diagnosticId, string message)
    {
        return CreateDisabled(
            metadata.PluginId,
            metadata.DisplayName,
            metadata.Version,
            metadata.SupportedApiVersion,
            diagnosticId,
            message);
    }

    public static PluginStatus CreateDisabled(PluginMetadata metadata, IReadOnlyList<DiagnosticInfo> diagnostics)
    {
        return new PluginStatus
        {
            PluginId = metadata.PluginId,
            DisplayName = metadata.DisplayName,
            Version = metadata.Version,
            SupportedApiVersion = metadata.SupportedApiVersion,
            Enabled = false,
            Diagnostics = diagnostics,
        };
    }

    public static PluginStatus CreateDisabled(
        string pluginId,
        string displayName,
        string version,
        string supportedApiVersion,
        string message)
    {
        return CreateDisabled(
            pluginId,
            displayName,
            version,
            supportedApiVersion,
            PluginDiagnosticIds.Load,
            message);
    }

    public static PluginStatus CreateDisabled(
        string pluginId,
        string displayName,
        string version,
        string supportedApiVersion,
        string diagnosticId,
        string message)
    {
        return new PluginStatus
        {
            PluginId = pluginId,
            DisplayName = displayName,
            Version = version,
            SupportedApiVersion = supportedApiVersion,
            Enabled = false,
            Diagnostics = [CreateDiagnostic(diagnosticId, DiagnosticSeverity.Error, message)],
        };
    }

    public static DiagnosticInfo CreateDiagnostic(string id, DiagnosticSeverity severity, string message)
    {
        return new DiagnosticInfo
        {
            Id = id,
            Severity = severity,
            Message = message,
        };
    }
}
