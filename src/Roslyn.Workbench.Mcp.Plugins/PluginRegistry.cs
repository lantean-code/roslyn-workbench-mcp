using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins;

public sealed class PluginRegistry : IPluginRegistry
{
    private readonly PluginMetadata _pluginMetadata;
    private readonly List<RegisteredTool> _registeredTools;
    private readonly List<IRegisteredPluginTool> _registeredPluginTools;
    private readonly HashSet<string> _toolNames;

    public PluginRegistry(PluginMetadata pluginMetadata)
    {
        _pluginMetadata = pluginMetadata;
        _registeredTools = [];
        _registeredPluginTools = [];
        _toolNames = new HashSet<string>(StringComparer.Ordinal);

        ValidatePluginMetadata(pluginMetadata);
    }

    public IReadOnlyList<RegisteredTool> RegisteredTools => _registeredTools;

    internal IReadOnlyList<IRegisteredPluginTool> RegisteredPluginTools => _registeredPluginTools;

    internal IRegisteredPluginTool GetRegisteredPluginTool(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        return _registeredPluginTools.Single(tool => string.Equals(tool.Tool.Metadata.Name, toolName, StringComparison.Ordinal));
    }

    internal IRegisteredPluginTool GetRegisteredPluginTool(RegisteredTool tool)
    {

        return _registeredPluginTools.Single(candidate => ReferenceEquals(candidate.Tool, tool));
    }

    public void RegisterQueryTool<TRequest, TResponse>(
        ToolRegistrationMetadata metadata,
        IQueryToolHandler<TRequest, TResponse> handler)
        where TRequest : WorkspaceBoundRequest
    {
        ArgumentNullException.ThrowIfNull(handler);

        var tool = CreateTool(metadata, ToolKind.Query, typeof(TRequest), typeof(TResponse));
        RegisterTool(new PluginQueryRegistration<TRequest, TResponse>(tool, handler));
    }

    public void RegisterMutationTool<TRequest>(
        ToolRegistrationMetadata metadata,
        IMutationToolHandler<TRequest> handler)
        where TRequest : WorkspaceBoundRequest
    {
        ArgumentNullException.ThrowIfNull(handler);

        var tool = CreateTool(metadata, ToolKind.Mutation, typeof(TRequest), typeof(MutationData));
        RegisterTool(new PluginMutationRegistration<TRequest>(tool, handler));
    }

    private void RegisterTool(IRegisteredPluginTool registration)
    {
        _registeredTools.Add(registration.Tool);
        _registeredPluginTools.Add(registration);
    }

    private RegisteredTool CreateTool(
        ToolRegistrationMetadata metadata,
        ToolKind kind,
        Type requestType,
        Type responseType)
    {
        ValidateToolRegistration(metadata, requestType, responseType);

        return new RegisteredTool
        {
            Plugin = _pluginMetadata,
            Metadata = metadata,
            Kind = kind,
            RequestType = requestType,
            ResponseType = responseType,
        };
    }

    private void ValidateToolRegistration(
        ToolRegistrationMetadata metadata,
        Type requestType,
        Type responseType)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        ValidateToolMetadata(metadata);
        ValidateContractType(requestType, nameof(requestType));
        ValidateContractType(responseType, nameof(responseType));

        if (!_toolNames.Add(metadata.Name))
        {
            throw new InvalidOperationException($"Tool name '{metadata.Name}' is already registered for plugin '{_pluginMetadata.PluginId}'.");
        }
    }

    private static void ValidatePluginMetadata(PluginMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (string.IsNullOrWhiteSpace(metadata.PluginId)
            || string.IsNullOrWhiteSpace(metadata.DisplayName)
            || string.IsNullOrWhiteSpace(metadata.Version))
        {
            throw new InvalidOperationException("Plugin metadata must provide PluginId, DisplayName, and Version.");
        }

        if (!string.Equals(metadata.SupportedApiVersion, PluginApiVersions.V1, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Plugin '{metadata.PluginId}' declares unsupported API version '{metadata.SupportedApiVersion}'.");
        }
    }

    private static void ValidateToolMetadata(ToolRegistrationMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.Name)
            || string.IsNullOrWhiteSpace(metadata.Title)
            || string.IsNullOrWhiteSpace(metadata.Description))
        {
            throw new InvalidOperationException("Tool metadata must provide Name, Title, and Description.");
        }
    }

    private static void ValidateContractType(Type type, string parameterName)
    {

        if (!type.IsPublic && !type.IsNestedPublic)
        {
            throw new InvalidOperationException($"Tool contract type '{type.FullName}' must be public.");
        }
    }
}
