using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal sealed class CodeActionMcpServerToolFactory : ICodeActionToolRegistrationVisitor<McpServerToolBase>
{
    private readonly ICodeActionExecutionContextFactory _contextFactory;
    private readonly IMcpToolProtocolFactory _protocolFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ToolOutputSchemaMode _outputSchemaMode;

    public CodeActionMcpServerToolFactory(
        IServiceProvider serviceProvider,
        ICodeActionExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory,
        ToolOutputSchemaMode outputSchemaMode = ToolOutputSchemaMode.Omit)
    {
        _serviceProvider = serviceProvider;
        _contextFactory = contextFactory;
        _protocolFactory = protocolFactory;
        _outputSchemaMode = outputSchemaMode;
    }

    public McpServerToolBase VisitQuery<THandler, TRequest, TResponse>(CodeActionQueryRegistration<THandler, TRequest, TResponse> registration)
        where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
        where TRequest : WorkspaceBoundRequest
    {
        return new CodeActionQueryMcpServerTool<THandler, TRequest, TResponse>(
            registration,
            ActivatorUtilities.CreateInstance<THandler>(_serviceProvider),
            _contextFactory,
            _protocolFactory,
            Options.Create(new StartupOptions
            {
                ToolOutputSchemaMode = _outputSchemaMode,
            }));
    }

    public McpServerToolBase VisitMutation<THandler, TRequest>(CodeActionMutationRegistration<THandler, TRequest> registration)
        where THandler : class, ICodeActionMutationToolHandler<TRequest>
        where TRequest : WorkspaceBoundRequest
    {
        return new CodeActionMutationMcpServerTool<THandler, TRequest>(
            registration,
            ActivatorUtilities.CreateInstance<THandler>(_serviceProvider),
            _contextFactory,
            _protocolFactory,
            Options.Create(new StartupOptions
            {
                ToolOutputSchemaMode = _outputSchemaMode,
            }));
    }
}
