using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class ConvertToInterpolatedStringTool : MutationToolHandler<ConvertToInterpolatedStringRequest, MutationProposal>
{
    private const string Title = "Convert to interpolated string";
    private const string EquivalenceKey = "Convert_to_interpolated_string";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-to-interpolated-string",
        Title = "Convert To Interpolated String",
        Description = "Converts a supported string expression to an interpolated string through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertToInterpolatedStringTool());
    }

    protected override async ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(ConvertToInterpolatedStringRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshotRejection = ToolExecutionHelpers.ValidateSnapshot<MutationProposal>(context, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Selection is null)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("InvalidRequest", "A location selector is required.");
        }

        var locationResolution = await context.Resolver.ResolveLocationAsync(request.Selection, cancellationToken).ConfigureAwait(false);
        if (locationResolution.Status != SelectorResolveStatus.Resolved)
        {
            return ToolExecutionHelpers.RejectFromStatus<MutationProposal>(locationResolution.Status, "Location");
        }

        return await context.CodeActionService.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = request.Selection,
            ExpectedSnapshot = request.ExpectedSnapshot,
            Title = Title,
            EquivalenceKey = EquivalenceKey,
        }, context, cancellationToken).ConfigureAwait(false);
    }
}
