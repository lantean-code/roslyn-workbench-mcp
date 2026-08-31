using System.Reflection;

namespace Roslyn.Workbench.Mcp.Plugins.Preparation;

/// <summary>
/// Merges handler attributes with fluent overrides into the effective tool registration metadata.
/// </summary>
internal static class PluginToolMetadataFactory
{
    /// <summary>
    /// Creates effective metadata, giving explicit builder values precedence over handler attributes.
    /// </summary>
    /// <param name="definition">The configured handler and its builder state.</param>
    /// <returns>The metadata used to validate and publish the tool.</returns>
    public static ToolRegistrationMetadata Create(ConfiguredToolDefinition definition)
    {
        var attribute = definition.HandlerType.GetCustomAttribute<RoslynToolAttribute>();
        var builder = definition.Builder;
        var metadata = new ToolRegistrationMetadata
        {
            Name = builder.Name ?? attribute?.Name ?? string.Empty,
            Title = builder.Title ?? attribute?.Title ?? string.Empty,
            Description = builder.Description ?? attribute?.Description ?? string.Empty,
            ResultSummary = builder.ResultSummary ?? attribute?.ResultSummary,
            Behavior = new ToolBehaviorHints
            {
                Destructive = builder.Destructive ?? attribute?.Destructive ?? false,
            },
        };

        return metadata;
    }
}
