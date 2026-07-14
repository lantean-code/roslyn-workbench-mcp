using System.Reflection;

namespace Roslyn.Workbench.Mcp.Plugins;

internal static class PluginToolMetadataFactory
{
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
