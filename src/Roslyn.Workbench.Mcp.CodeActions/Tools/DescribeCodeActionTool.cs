using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class DescribeCodeActionTool : CodeActionQueryToolHandler<DescribeCodeActionRequest, DescribeCodeActionData>
{
    private readonly ICodeActionComposition _composition;
    private readonly ICodeActionResolver _resolver;
    private readonly ICodeActionInfoFactory _infoFactory;

    public DescribeCodeActionTool(
        ICodeActionComposition composition,
        ICodeActionResolver resolver,
        ICodeActionInfoFactory infoFactory)
    {
        _composition = composition;
        _resolver = resolver;
        _infoFactory = infoFactory;
    }

    protected override async ValueTask<CodeActionExecutionResult<DescribeCodeActionData>> ExecuteCoreAsync(
        DescribeCodeActionRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken)
    {
        if (!_composition.Status.IsAvailable)
        {
            return CodeActionsUnavailable<DescribeCodeActionData>();
        }

        var resolvedAction = await _resolver.ResolveActionAsync<DescribeCodeActionData>(
            request.ActionId,
            request.ExpectedSnapshot,
            context,
            cancellationToken);

        if (resolvedAction.HasRejection)
        {
            return resolvedAction.Rejection;
        }

        var syntaxTree = await resolvedAction.Document.GetSyntaxTreeAsync(cancellationToken);
        if (syntaxTree is null)
        {
            return Rejected<DescribeCodeActionData>(
                "ActionUnavailable",
                "The selected action location could not be resolved.",
                RequiredAction.ResolveTargetAgain);
        }

        var sourceLocation = syntaxTree.GetLocation(resolvedAction.Span);
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(sourceLocation);
        if (resolvedLocation is null)
        {
            return Rejected<DescribeCodeActionData>(
                "ActionUnavailable",
                "The selected action location could not be resolved.",
                RequiredAction.ResolveTargetAgain);
        }

        var descriptor = _infoFactory.CreateFromReference(
            resolvedAction.Action,
            context,
            resolvedAction.Descriptor,
            resolvedAction.Reference,
            resolvedLocation);

        var descriptorContext = new CodeActionDescriptorContext
        {
            Kind = resolvedAction.Descriptor.ContextKind,
            Message = resolvedAction.Descriptor.Message,
        };

        var data = new DescribeCodeActionData
        {
            Descriptor = descriptor,
            Context = descriptorContext,
        };

        return CodeActionExecutionResult.Success(data);
    }
}
