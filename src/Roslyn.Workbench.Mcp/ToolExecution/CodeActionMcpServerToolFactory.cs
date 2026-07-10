using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal sealed class CodeActionMcpServerToolFactory : ICodeActionToolRegistrationVisitor<McpServerToolBase>
{
    private readonly ICodeActionExecutionContextFactory _contextFactory;
    private readonly ToolOutputSchemaMode _outputSchemaMode;

    public CodeActionMcpServerToolFactory(
        ICodeActionExecutionContextFactory contextFactory,
        ToolOutputSchemaMode outputSchemaMode = ToolOutputSchemaMode.Omit)
    {
        _contextFactory = contextFactory;
        _outputSchemaMode = outputSchemaMode;
    }

    public McpServerToolBase VisitQuery<TRequest, TResponse>(CodeActionQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest
    {
        return new CodeActionQueryMcpServerTool<TRequest, TResponse>(
            McpToolProtocolFactory.CreateCodeActionTool<TRequest>(
                registration.Metadata,
                registration.Kind,
                registration.ResponseType,
                _outputSchemaMode),
            registration.Handler,
            _contextFactory);
    }

    public McpServerToolBase VisitMutation<TRequest>(CodeActionMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceBoundRequest
    {
        return new CodeActionMutationMcpServerTool<TRequest>(
            McpToolProtocolFactory.CreateCodeActionTool<TRequest>(
                registration.Metadata,
                registration.Kind,
                typeof(MutationData),
                _outputSchemaMode),
            registration.Metadata,
            registration.Handler,
            _contextFactory);
    }
}
