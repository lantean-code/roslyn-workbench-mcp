using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertToInterpolatedStringTool : CodeActionMutationToolHandler<ConvertToInterpolatedStringRequest>
{
    private const string Title = "Convert to interpolated string";
    private const string EquivalenceKey = "Convert_to_interpolated_string";

    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "convert-to-interpolated-string",
        Title = "Convert To Interpolated String",
        Description = "Converts a supported string expression to an interpolated string through Roslyn refactoring composition.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertToInterpolatedStringTool());
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteCoreAsync(ConvertToInterpolatedStringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {

        var snapshotRejection = ToolExecutionHelpers.ValidateSnapshot<WorkspaceMutationProposal>(context, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Selection is null)
        {
            return ToolExecutionHelpers.Rejected<WorkspaceMutationProposal>("InvalidRequest", "A location selector is required.");
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(request.Selection, cancellationToken).ConfigureAwait(false);
        if (locationResolution.Status != SelectorResolveStatus.Resolved)
        {
            return ToolExecutionHelpers.RejectFromStatus<WorkspaceMutationProposal>(locationResolution.Status, "Location");
        }

        return await context.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = request.Selection,
            ExpectedSnapshot = request.ExpectedSnapshot,
            Title = Title,
            EquivalenceKey = EquivalenceKey,
        }, cancellationToken).ConfigureAwait(false);
    }
}
