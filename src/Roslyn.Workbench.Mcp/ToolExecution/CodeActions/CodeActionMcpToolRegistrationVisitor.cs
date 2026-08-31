namespace Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

/// <summary>
/// Registers Code Action catalogue entries and their executable MCP wrappers with dependency injection.
/// </summary>
internal sealed class CodeActionMcpToolRegistrationVisitor : ICodeActionToolRegistrationVisitor<bool>
{
    private readonly IServiceCollection _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionMcpToolRegistrationVisitor"/> class.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public CodeActionMcpToolRegistrationVisitor(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Registers a Code Action query handler, its metadata, and its MCP server tool.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="registration">The query registration to add to dependency injection.</param>
    /// <returns><see langword="true"/> after the registration has been added.</returns>
    public bool VisitQuery<THandler, TRequest, TResponse>(CodeActionQueryRegistration<THandler, TRequest, TResponse> registration)
        where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
        where TRequest : WorkspaceBoundRequest
    {
        _services.AddSingleton(registration);
        _services.AddSingleton<THandler>();
        _services.AddSingleton<McpServerTool, CodeActionQueryMcpServerTool<THandler, TRequest, TResponse>>();
        return true;
    }

    /// <summary>
    /// Registers a Code Action mutation handler, its metadata, and its MCP server tool.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="registration">The mutation registration to add to dependency injection.</param>
    /// <returns><see langword="true"/> after the registration has been added.</returns>
    public bool VisitMutation<THandler, TRequest>(CodeActionMutationRegistration<THandler, TRequest> registration)
        where THandler : class, ICodeActionMutationToolHandler<TRequest>
        where TRequest : WorkspaceMutationRequest
    {
        _services.AddSingleton(registration);
        _services.AddSingleton<THandler>();
        _services.AddSingleton<McpServerTool, CodeActionMutationMcpServerTool<THandler, TRequest>>();
        return true;
    }
}
