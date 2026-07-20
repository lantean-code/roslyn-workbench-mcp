using static Roslyn.Workbench.Mcp.CodeActions.Execution.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class DescribeCodeActionTool : CodeActionQueryToolHandler<DescribeCodeActionRequest, DescribeCodeActionData>
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionResolutionService _resolutionService;
    private readonly ICodeActionInfoFactory _infoFactory;

    public DescribeCodeActionTool(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionResolutionService resolutionService,
        ICodeActionInfoFactory infoFactory)
    {
        _providerCatalog = providerCatalog;
        _resolutionService = resolutionService;
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

        var resolvedAction = await _resolutionService.ResolveActionAsync<DescribeCodeActionData>(
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
