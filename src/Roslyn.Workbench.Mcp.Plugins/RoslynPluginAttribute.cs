using System.Composition;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Marks the single MEF entry point supplied by a Roslyn Workbench plugin assembly.
/// </summary>
[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RoslynPluginAttribute : ExportAttribute
{
    /// <summary>
    /// Gets the stable plugin identifier.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets the plugin display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the exact Roslyn Workbench plugin API version supported by the plugin.
    /// </summary>
    public string SupportedApiVersion { get; }

    /// <summary>
    /// Initialises a plugin export.
    /// </summary>
    /// <param name="pluginId">The stable plugin identifier.</param>
    /// <param name="displayName">The plugin display name.</param>
    /// <param name="supportedApiVersion">The exact supported API version.</param>
    public RoslynPluginAttribute(string pluginId, string displayName, string supportedApiVersion)
        : base(typeof(IRoslynPlugin))
    {
        PluginId = pluginId;
        DisplayName = displayName;
        SupportedApiVersion = supportedApiVersion;
    }
}
