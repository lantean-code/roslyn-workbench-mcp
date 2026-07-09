using Roslyn.Workbench.Mcp.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class MoveTypeToFileTool : CodeActionMutationToolHandler<MoveTypeToFileRequest>
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

    protected override async ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(MoveTypeToFileRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (!request.PreserveNamespace)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("UnsupportedOption", "The preserveNamespace option must remain true for the current move-type-to-file backend.");
        }

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<MutationProposal>(request.Type, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
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

        var locationSelector = ToolExecutionHelpers.CreateLocationSelector(context.WorkspaceResolver.CreateResolvedLocation(sourceLocation));
        if (locationSelector is null)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("SymbolNotSupported", "The selected symbol does not resolve to a replayable source span.", RequiredAction.ResolveTargetAgain);
        }

        return await context.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = locationSelector,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = ProviderId,
            TitleStartsWith = TitlePrefix,
        }, cancellationToken).ConfigureAwait(false);
    }
}
