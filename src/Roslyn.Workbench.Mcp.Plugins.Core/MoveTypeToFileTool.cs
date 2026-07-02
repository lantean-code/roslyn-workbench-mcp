using Roslyn.Workbench.Mcp.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class MoveTypeToFileTool : MutationToolHandler<MoveTypeToFileRequest, MutationProposal>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CodeRefactorings.MoveType.MoveTypeCodeRefactoringProvider";
    private const string TitlePrefix = "Move type to ";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "move-type-to-file",
        Title = "Move Type To File",
        Description = "Moves one selected type into its own Roslyn-chosen file within the current project.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new MoveTypeToFileTool());
    }

    protected override async ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(MoveTypeToFileRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        if (!request.PreserveNamespace)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("UnsupportedOption", "The preserveNamespace option must remain true for the current move-type-to-file backend.");
        }

        var symbolResolution = await ToolExecutionHelpers.ResolveSymbolAsync<MutationProposal>(request.Type, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol typeSymbol)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("SymbolNotSupported", "The selected symbol is not a type declaration.", RequiredAction.ResolveTargetAgain);
        }

        var sourceLocation = typeSymbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is null)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("SymbolNotSupported", "The selected symbol does not resolve to a source location.", RequiredAction.ResolveTargetAgain);
        }

        var locationSelector = ToolExecutionHelpers.CreateLocationSelector(context.Resolver.CreateResolvedLocation(sourceLocation));
        if (locationSelector is null)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("SymbolNotSupported", "The selected symbol does not resolve to a replayable source span.", RequiredAction.ResolveTargetAgain);
        }

        return await context.CodeActionService.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = locationSelector,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = ProviderId,
            TitleStartsWith = TitlePrefix,
        }, context, cancellationToken).ConfigureAwait(false);
    }
}
