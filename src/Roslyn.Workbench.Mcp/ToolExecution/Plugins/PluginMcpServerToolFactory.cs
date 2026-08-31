using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.Plugins;

/// <summary>
/// Creates executable MCP wrappers for registered plugin tools.
/// </summary>
internal sealed class PluginMcpServerToolFactory : IPluginMcpServerToolFactory
{
    private readonly IToolExecutionContextFactory _contextFactory;
    private readonly IMcpToolProtocolFactory _protocolFactory;
    private readonly IToolRequestBinder _requestBinder;
    private readonly IOptions<StartupOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginMcpServerToolFactory"/> class.
    /// </summary>
    /// <param name="contextFactory">The factory that acquires workspace-scoped execution contexts.</param>
    /// <param name="protocolFactory">The factory that creates published MCP tool definitions.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="options">The Host settings that control schema publication.</param>
    public PluginMcpServerToolFactory(
        IToolExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IOptions<StartupOptions> options)
    {
        _contextFactory = contextFactory;
        _protocolFactory = protocolFactory;
        _requestBinder = requestBinder;
        _options = options;
    }

    /// <summary>
    /// Creates the executable MCP wrapper for a plugin query registration.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="registration">The query contract, handler, and catalogue metadata.</param>
    /// <returns>The MCP server tool.</returns>
    public McpServerTool VisitQuery<TRequest, TResponse>(PluginQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest
        where TResponse : IQueryResponse
    {
        return new PluginQueryMcpServerTool<TRequest, TResponse>(
            registration,
            _contextFactory,
            _protocolFactory,
            _requestBinder,
            _options);
    }

    /// <summary>
    /// Creates the executable MCP wrapper for a plugin mutation registration.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="registration">The mutation contract, handler, and catalogue metadata.</param>
    /// <returns>The MCP server tool.</returns>
    public McpServerTool VisitMutation<TRequest>(PluginMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceMutationRequest
    {
        return new PluginMutationMcpServerTool<TRequest>(
            registration,
            _contextFactory,
            _protocolFactory,
            _requestBinder,
            _options);
    }
}
