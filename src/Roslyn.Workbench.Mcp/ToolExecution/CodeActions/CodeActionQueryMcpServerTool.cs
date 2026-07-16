using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

internal sealed class CodeActionQueryMcpServerTool<THandler, TRequest, TResponse> : McpServerToolBase
    where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
    where TRequest : WorkspaceBoundRequest
{
    private readonly THandler _handler;
    private readonly ICodeActionExecutionContextFactory _contextFactory;

    public CodeActionQueryMcpServerTool(
        CodeActionQueryRegistration<THandler, TRequest, TResponse> registration,
        THandler handler,
        ICodeActionExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory,
        IOptions<StartupOptions> options)
        : base(protocolFactory.CreateCodeActionTool<TRequest>(
            registration.Metadata,
            registration.Kind,
            registration.ResponseType,
            options.Value.ToolOutputSchemaMode))
    {
        _handler = handler;
        _contextFactory = contextFactory;
    }

    protected override async ValueTask<CallToolResult> InvokeCoreAsync(
        IDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var request = ToolRequestBinder.Deserialize<TRequest>(arguments);
        var contextLease = _contextFactory.CreateQueryContext(request, cancellationToken);
        await using var _ = contextLease.ConfigureAwait(false);
        if (contextLease.HasFailure)
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializeCodeActionFailure(contextLease.Failure),
                isError: true);
        }

        var context = contextLease.Context;
        var result = await _handler.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);
        return CreateStructuredResult(
            McpPublishedResultSerializer.SerializeCodeActionQuery(result),
            result.Outcome.IsError());
    }
}
