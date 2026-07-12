using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal sealed class CodeActionMcpToolRegistrationVisitor : ICodeActionToolRegistrationVisitor<bool>
{
    private readonly IServiceCollection _services;
    private readonly ToolOutputSchemaMode _outputSchemaMode;

    public CodeActionMcpToolRegistrationVisitor(
        IServiceCollection services,
        ToolOutputSchemaMode outputSchemaMode)
    {
        _services = services;
        _outputSchemaMode = outputSchemaMode;
    }

    public bool VisitQuery<TRequest, TResponse>(CodeActionQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest
    {
        var protocolTool = McpToolProtocolFactory.CreateCodeActionTool<TRequest>(
            registration.Metadata,
            registration.Kind,
            registration.ResponseType,
            _outputSchemaMode);
        _services.AddSingleton<McpServerTool>(serviceProvider => new CodeActionQueryMcpServerTool<TRequest, TResponse>(
            protocolTool,
            registration.Handler,
            serviceProvider.GetRequiredService<ICodeActionExecutionContextFactory>()));
        return true;
    }

    public bool VisitMutation<TRequest>(CodeActionMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceBoundRequest
    {
        var protocolTool = McpToolProtocolFactory.CreateCodeActionTool<TRequest>(
            registration.Metadata,
            registration.Kind,
            registration.ResponseType,
            _outputSchemaMode);
        _services.AddSingleton<McpServerTool>(serviceProvider => new CodeActionMutationMcpServerTool<TRequest>(
            protocolTool,
            registration.Metadata,
            registration.Handler,
            serviceProvider.GetRequiredService<ICodeActionExecutionContextFactory>()));
        return true;
    }
}
