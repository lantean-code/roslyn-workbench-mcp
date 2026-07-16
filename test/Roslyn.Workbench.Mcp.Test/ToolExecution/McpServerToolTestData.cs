using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

internal static class McpServerToolTestData
{
    public static Dictionary<string, JsonElement> CreateArguments(bool includeWorkspace = false)
    {
        var arguments = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        };

        if (includeWorkspace)
        {
            arguments["workspace"] = JsonSerializer.SerializeToElement(new WorkspaceSelector
            {
                WorkspaceId = "WorkspaceId",
            });
        }

        return arguments;
    }

    public static Tool CreateProtocolTool(string name)
    {
        return new Tool
        {
            Name = name,
            Description = "Description",
        };
    }

    public static PluginQueryRegistration<TRequest, TResponse> CreatePluginQueryRegistration<TRequest, TResponse>(
        IQueryToolHandler<TRequest, TResponse> handler,
        string name)
        where TRequest : WorkspaceBoundRequest
    {
        return new PluginQueryRegistration<TRequest, TResponse>(
            CreateRegisteredPluginTool(name, ToolKind.Query, typeof(TRequest), typeof(TResponse)),
            handler);
    }

    public static PluginMutationRegistration<TRequest> CreatePluginMutationRegistration<TRequest>(
        IMutationToolHandler<TRequest> handler,
        string name)
        where TRequest : WorkspaceBoundRequest
    {
        return new PluginMutationRegistration<TRequest>(
            CreateRegisteredPluginTool(name, ToolKind.Mutation, typeof(TRequest), typeof(MutationData)),
            handler);
    }

    public static Mock<IMcpToolProtocolFactory> CreateProtocolFactory(Tool protocolTool)
    {
        var protocolFactory = new Mock<IMcpToolProtocolFactory>();
        protocolFactory.SetReturnsDefault(protocolTool);
        return protocolFactory;
    }

    public static IOptions<StartupOptions> CreateOptions(
        ToolOutputSchemaMode outputSchemaMode = ToolOutputSchemaMode.Omit)
    {
        return Options.Create(new StartupOptions
        {
            ToolOutputSchemaMode = outputSchemaMode,
        });
    }

    private static RegisteredTool CreateRegisteredPluginTool(
        string name,
        ToolKind kind,
        Type requestType,
        Type responseType)
    {
        return new RegisteredTool
        {
            Plugin = new PluginMetadata
            {
                PluginId = "plugin.test",
                DisplayName = "Plugin Test",
                Version = "1.0.0",
                SupportedApiVersion = PluginApiVersions.V1,
            },
            Metadata = new ToolRegistrationMetadata
            {
                Name = name,
                Title = "Title",
                Description = "Description",
            },
            Kind = kind,
            RequestType = requestType,
            ResponseType = responseType,
        };
    }
}
