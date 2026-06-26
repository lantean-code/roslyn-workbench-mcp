using System.Text.Json;

using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.Plugins;

public sealed class PluginRegistry : IPluginRegistry
{
    private readonly PluginMetadata _pluginMetadata;
    private readonly List<RegisteredTool> _registeredTools;
    private readonly HashSet<string> _toolNames;

    public PluginRegistry(PluginMetadata pluginMetadata)
    {
        _pluginMetadata = pluginMetadata;
        _registeredTools = [];
        _toolNames = new HashSet<string>(StringComparer.Ordinal);

        ValidatePluginMetadata(pluginMetadata);
    }

    public IReadOnlyList<RegisteredTool> RegisteredTools => _registeredTools;

    public void RegisterQueryTool<TRequest, TResponse>(ToolRegistrationMetadata metadata, IQueryToolHandler<TRequest, TResponse> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        RegisterTool<TRequest, TResponse>(metadata, ToolKind.Query, new PluginToolInvoker<TRequest, TResponse>(handler));
    }

    public void RegisterMutationTool<TRequest, TResponse>(ToolRegistrationMetadata metadata, IMutationToolHandler<TRequest, TResponse> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        RegisterTool<TRequest, TResponse>(metadata, ToolKind.Mutation, new PluginToolInvoker<TRequest, TResponse>(handler));
    }

    private void RegisterTool<TRequest, TResponse>(ToolRegistrationMetadata metadata, ToolKind kind, IPluginToolInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(invoker);

        var requestType = typeof(TRequest);
        var responseType = typeof(TResponse);

        ValidateToolMetadata(metadata);
        ValidateContractType(requestType, nameof(requestType));
        ValidateContractType(responseType, nameof(responseType));

        if (!_toolNames.Add(metadata.Name))
        {
            throw new InvalidOperationException($"Tool name '{metadata.Name}' is already registered for plugin '{_pluginMetadata.PluginId}'.");
        }

        if (kind == ToolKind.Mutation && responseType != typeof(MutationProposal))
        {
            throw new InvalidOperationException(
                $"Mutation tool '{metadata.Name}' must return '{typeof(MutationProposal).FullName}'.");
        }

        _registeredTools.Add(new RegisteredTool
        {
            Plugin = _pluginMetadata,
            Metadata = metadata,
            Kind = kind,
            RequestType = requestType,
            ResponseType = kind == ToolKind.Mutation ? typeof(Contracts.Results.MutationData) : responseType,
            InputSchema = ToolSchemaFactory.CreateInputSchema<TRequest>(),
            OutputSchema = kind == ToolKind.Mutation
                ? ToolSchemaFactory.CreateToolResultSchema<Contracts.Results.MutationData>()
                : ToolSchemaFactory.CreateToolResultSchema<TResponse>(),
            Annotations = CreateAnnotations(kind, metadata),
            Invoker = invoker,
        });
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
