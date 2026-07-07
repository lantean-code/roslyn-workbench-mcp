using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Plugins.Protocol;

namespace Roslyn.Workbench.Mcp.Plugins;

public sealed class PluginRegistry : IPluginRegistry
{
    private readonly PluginMetadata _pluginMetadata;
    private readonly ToolOutputSchemaMode _outputSchemaMode;
    private readonly List<RegisteredTool> _registeredTools;
    private readonly List<RegisteredPluginTool> _registeredPluginTools;
    private readonly HashSet<string> _toolNames;

    public PluginRegistry(PluginMetadata pluginMetadata)
        : this(pluginMetadata, ToolOutputSchemaMode.Omit)
    {
    }

    public PluginRegistry(PluginMetadata pluginMetadata, ToolOutputSchemaMode outputSchemaMode)
    {
        _pluginMetadata = pluginMetadata;
        _outputSchemaMode = outputSchemaMode;
        _registeredTools = [];
        _registeredPluginTools = [];
        _toolNames = new HashSet<string>(StringComparer.Ordinal);

        ValidatePluginMetadata(pluginMetadata);
    }

    public IReadOnlyList<RegisteredTool> RegisteredTools => _registeredTools;

    internal IReadOnlyList<RegisteredPluginTool> RegisteredPluginTools => _registeredPluginTools;

    internal RegisteredPluginTool GetRegisteredPluginTool(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        return _registeredPluginTools.Single(tool => string.Equals(tool.Tool.Metadata.Name, toolName, StringComparison.Ordinal));
    }

    internal RegisteredPluginTool GetRegisteredPluginTool(RegisteredTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        return _registeredPluginTools.Single(candidate => ReferenceEquals(candidate.Tool, tool));
    }

    public void RegisterQueryTool<TRequest, TResponse>(ToolRegistrationMetadata metadata, IQueryToolHandler<TRequest, TResponse> handler)
        where TRequest : WorkspaceBoundRequest
    {
        ArgumentNullException.ThrowIfNull(handler);

        RegisterTool(CreateQueryTool(metadata, handler));
    }

    public void RegisterMutationTool<TRequest>(
        ToolRegistrationMetadata metadata,
        IMutationToolHandler<TRequest> handler)
        where TRequest : WorkspaceBoundRequest
    {
        ArgumentNullException.ThrowIfNull(handler);

        RegisterTool(CreateMutationTool(metadata, handler));
    }

    private void RegisterTool(RegisteredPluginTool pluginTool)
    {
        _registeredTools.Add(pluginTool.Tool);
        _registeredPluginTools.Add(pluginTool);
    }

    private RegisteredPluginTool CreateQueryTool<TRequest, TResponse>(
        ToolRegistrationMetadata metadata,
        IQueryToolHandler<TRequest, TResponse> handler)
        where TRequest : WorkspaceBoundRequest
    {
        var requestType = typeof(TRequest);
        var responseType = typeof(TResponse);

        ValidateToolRegistration(metadata, ToolKind.Query, requestType, responseType);

        var tool = new RegisteredTool
        {
            Plugin = _pluginMetadata,
            Metadata = metadata,
            Kind = ToolKind.Query,
            RequestType = requestType,
            InputSchema = ToolSchemaFactory.CreateInputSchema<TRequest>(),
            OutputSchema = _outputSchemaMode == ToolOutputSchemaMode.Full
                ? ToolSchemaFactory.CreateOutputSchema(ToolKind.Query, responseType)
                : null,
            Annotations = CreateAnnotations(ToolKind.Query, metadata),
        };

        return new RegisteredPluginTool
        {
            Tool = tool,
            Runtime = new QueryPluginToolRuntime<TRequest, TResponse>(handler),
        };
    }

    private RegisteredPluginTool CreateMutationTool<TRequest>(
        ToolRegistrationMetadata metadata,
        IMutationToolHandler<TRequest> handler)
        where TRequest : WorkspaceBoundRequest
    {
        var requestType = typeof(TRequest);
        var responseType = typeof(MutationProposal);

        ValidateToolRegistration(metadata, ToolKind.Mutation, requestType, responseType);

        var tool = new RegisteredTool
        {
            Plugin = _pluginMetadata,
            Metadata = metadata,
            Kind = ToolKind.Mutation,
            RequestType = requestType,
            InputSchema = ToolSchemaFactory.CreateInputSchema<TRequest>(),
            OutputSchema = _outputSchemaMode == ToolOutputSchemaMode.Full
                ? ToolSchemaFactory.CreateOutputSchema(ToolKind.Mutation, typeof(Contracts.Results.MutationData))
                : null,
            Annotations = CreateAnnotations(ToolKind.Mutation, metadata),
        };

        return new RegisteredPluginTool
        {
            Tool = tool,
            Runtime = new MutationPluginToolRuntime<TRequest>(tool, handler),
        };
    }

    private void ValidateToolRegistration(
        ToolRegistrationMetadata metadata,
        ToolKind kind,
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

        if (kind == ToolKind.Query && !QueryResponseContract.IsSupportedQueryResponseType(responseType))
        {
            throw new InvalidOperationException(
                $"Query tool '{metadata.Name}' must return '{typeof(QueryResponse<>).FullName}', '{typeof(CollectionResponse<>).FullName}', or a supported internal query response contract.");
        }

        if (kind == ToolKind.Mutation && responseType != typeof(MutationProposal))
        {
            throw new InvalidOperationException(
                $"Mutation tool '{metadata.Name}' must return '{typeof(MutationProposal).FullName}'.");
        }
    }

    private static ToolAnnotations CreateAnnotations(ToolKind kind, ToolRegistrationMetadata metadata)
    {
        return new ToolAnnotations
        {
            Title = metadata.Title,
            ReadOnlyHint = kind == ToolKind.Query,
            IdempotentHint = kind == ToolKind.Query,
            OpenWorldHint = false,
            DestructiveHint = kind == ToolKind.Mutation && metadata.Behavior.Destructive,
        };
    }

    private static void ValidatePluginMetadata(PluginMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.PluginId))
        {
            throw new InvalidOperationException("Plugin metadata must provide PluginId.");
        }

        if (string.IsNullOrWhiteSpace(metadata.DisplayName))
        {
            throw new InvalidOperationException("Plugin metadata must provide DisplayName.");
        }

        if (string.IsNullOrWhiteSpace(metadata.Version))
        {
            throw new InvalidOperationException("Plugin metadata must provide Version.");
        }

        if (!string.Equals(metadata.SupportedApiVersion, PluginApiVersions.V1, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Plugin '{metadata.PluginId}' declares unsupported API version '{metadata.SupportedApiVersion}'.");
        }
    }

    private static void ValidateToolMetadata(ToolRegistrationMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            throw new InvalidOperationException("Tool metadata must provide Name.");
        }

        if (string.IsNullOrWhiteSpace(metadata.Title))
        {
            throw new InvalidOperationException($"Tool '{metadata.Name}' must provide Title.");
        }

        if (string.IsNullOrWhiteSpace(metadata.Description))
        {
            throw new InvalidOperationException($"Tool '{metadata.Name}' must provide Description.");
        }
    }

    private static void ValidateContractType(Type contractType, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(contractType, parameterName);

        if (contractType.IsAbstract || contractType.IsInterface || contractType.ContainsGenericParameters)
        {
            throw new InvalidOperationException(
                $"Registered contract type '{contractType.FullName}' for '{parameterName}' must be a concrete closed type.");
        }
    }
}
