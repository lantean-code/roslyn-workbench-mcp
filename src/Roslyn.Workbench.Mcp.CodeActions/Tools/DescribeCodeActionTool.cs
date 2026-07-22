using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class DescribeCodeActionTool : CodeActionQueryToolHandler<DescribeCodeActionRequest, DescribeCodeActionData>
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionResolver _resolver;
    private readonly ICodeActionInfoFactory _infoFactory;

    public DescribeCodeActionTool(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionResolver resolver,
        ICodeActionInfoFactory infoFactory)
    {
        _providerCatalog = providerCatalog;
        _resolver = resolver;
        _infoFactory = infoFactory;
    }

    protected override async ValueTask<CodeActionExecutionResult<DescribeCodeActionData>> ExecuteCoreAsync(
        DescribeCodeActionRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken)
    {
        if (!_providerCatalog.Status.IsAvailable)
        {
            return CodeActionsUnavailable<DescribeCodeActionData>();
        }

        var resolvedAction = await _resolver.ResolveActionAsync<DescribeCodeActionData>(
            request.ActionId,
            request.ExpectedSnapshot,
            expectedKind: null,
            context,
            cancellationToken);

        if (resolvedAction.HasRejection)
        {
            return resolvedAction.Rejection;
        }

        var data = new DescribeCodeActionData
        {
            Descriptor = _infoFactory.Create(
                resolvedAction.Action,
                context,
                resolvedAction.Document,
                resolvedAction.Span,
                resolvedAction.Descriptor),
            Context = new CodeActionDescriptorContext
            {
                Kind = resolvedAction.Descriptor.ContextKind,
                Message = resolvedAction.Descriptor.Message,
            },
        };

        return CodeActionExecutionResult<DescribeCodeActionData>.Success(data);
    }
}
