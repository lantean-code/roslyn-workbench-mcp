using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal sealed class CodeActionMcpToolRegistrationVisitor : ICodeActionToolRegistrationVisitor<bool>
{
    private readonly IServiceCollection _services;

    public CodeActionMcpToolRegistrationVisitor(IServiceCollection services)
    {
        _services = services;
    }

    public bool VisitQuery<THandler, TRequest, TResponse>(CodeActionQueryRegistration<THandler, TRequest, TResponse> registration)
        where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
        where TRequest : WorkspaceBoundRequest
    {
        _services.AddSingleton(registration);
        _services.AddSingleton<THandler>();
        _services.AddSingleton<McpServerTool, CodeActionQueryMcpServerTool<THandler, TRequest, TResponse>>();
        return true;
    }

    public bool VisitMutation<THandler, TRequest>(CodeActionMutationRegistration<THandler, TRequest> registration)
        where THandler : class, ICodeActionMutationToolHandler<TRequest>
        where TRequest : WorkspaceBoundRequest
    {
        _services.AddSingleton(registration);
        _services.AddSingleton<THandler>();
        _services.AddSingleton<McpServerTool, CodeActionMutationMcpServerTool<THandler, TRequest>>();
        return true;
    }
}
