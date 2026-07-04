namespace Roslyn.Workbench.Mcp.Plugins;

internal sealed class PluginToolInvoker<TRequest, TResponse> : IPluginToolInvoker
{
    private readonly ToolKind _kind;
    private readonly IQueryToolHandler<TRequest, TResponse>? _queryHandler;
    private readonly IMutationToolHandler<TRequest, TResponse>? _mutationHandler;

    public PluginToolInvoker(IQueryToolHandler<TRequest, TResponse> handler)
    {
        _kind = ToolKind.Query;
        _queryHandler = handler;
    }

    public PluginToolInvoker(IMutationToolHandler<TRequest, TResponse> handler)
    {
        _kind = ToolKind.Mutation;
        _mutationHandler = handler;
    }

    public async ValueTask<PluginExecutionResultBox> ExecuteAsync(object request, IToolExecutionContext context, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        PluginExecutionResult<TResponse> result;

        switch (_kind)
        {
            case ToolKind.Query:
                result = await _queryHandler!.ExecuteAsync(typedRequest, (IQueryContext)context, cancellationToken);
                break;

            case ToolKind.Mutation:
                result = await _mutationHandler!.ExecuteAsync(typedRequest, (IMutationContext)context, cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unsupported tool kind '{_kind}'.");
        }

        return PluginExecutionResultBox.From(result);
    }
}
