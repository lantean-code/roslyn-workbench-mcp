namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Builds enabled and disabled catalogue entries from plugin metadata and load diagnostics.
/// </summary>
internal static class PluginCatalogStatusFactory
{
    /// <summary>
    /// Creates an enabled plugin status from validated metadata and diagnostics.
    /// </summary>
    /// <param name="metadata">The metadata that describes the relevant component.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <returns>An enabled status for the plugin.</returns>
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

    /// <summary>
    /// Creates a disabled plugin status containing the supplied metadata and diagnostics.
    /// </summary>
    /// <param name="metadata">The metadata that describes the relevant component.</param>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <returns>A disabled status explaining why the plugin is unavailable.</returns>
    public static PluginStatus CreateDisabled(PluginEntryPointMetadata metadata, string message)
    {
        return CreateDisabled(metadata.PluginId, metadata.DisplayName, metadata.Version, metadata.SupportedApiVersion, message);
    }

    /// <summary>
    /// Creates a disabled plugin status containing the supplied metadata and diagnostics.
    /// </summary>
    /// <param name="metadata">The metadata that describes the relevant component.</param>
    /// <param name="diagnosticId">The diagnostic identifier.</param>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <returns>A disabled status explaining why the plugin is unavailable.</returns>
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

    /// <summary>
    /// Creates a disabled plugin status containing the supplied metadata and diagnostics.
    /// </summary>
    /// <param name="metadata">The metadata that describes the relevant component.</param>
    /// <param name="diagnosticId">The diagnostic identifier.</param>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <returns>A disabled status explaining why the plugin is unavailable.</returns>
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

    /// <summary>
    /// Creates a disabled plugin status containing the supplied metadata and diagnostics.
    /// </summary>
    /// <param name="metadata">The metadata that describes the relevant component.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <returns>A disabled status explaining why the plugin is unavailable.</returns>
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

    /// <summary>
    /// Creates a disabled plugin status containing the supplied metadata and diagnostics.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="displayName">The user-facing name of the plugin package or component.</param>
    /// <param name="version">The version used to identify the relevant state.</param>
    /// <param name="supportedApiVersion">The API version supported by the plugin package.</param>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <returns>A disabled status explaining why the plugin is unavailable.</returns>
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

    /// <summary>
    /// Creates a disabled plugin status containing the supplied metadata and diagnostics.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="displayName">The user-facing name of the plugin package or component.</param>
    /// <param name="version">The version used to identify the relevant state.</param>
    /// <param name="supportedApiVersion">The API version supported by the plugin package.</param>
    /// <param name="diagnosticId">The diagnostic identifier.</param>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <returns>A disabled status explaining why the plugin is unavailable.</returns>
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
            PluginId = NullIfEmpty(pluginId),
            DisplayName = NullIfEmpty(displayName),
            Version = NullIfEmpty(version),
            SupportedApiVersion = NullIfEmpty(supportedApiVersion),
            Enabled = false,
            Diagnostics = [CreateDiagnostic(diagnosticId, DiagnosticSeverity.Error, message)],
        };
    }

    /// <summary>
    /// Creates a plugin diagnostic with the supplied identity, severity, and message.
    /// </summary>
    /// <param name="id">The stable identifier of the resulting diagnostic or component.</param>
    /// <param name="severity">The severity assigned to the resulting diagnostic.</param>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <returns>The plugin diagnostic.</returns>
    public static DiagnosticInfo CreateDiagnostic(string id, DiagnosticSeverity severity, string message)
    {
        return new DiagnosticInfo
        {
            Id = id,
            Severity = severity,
            Message = message,
        };
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
