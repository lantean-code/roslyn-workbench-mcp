using Roslyn.Workbench.Mcp.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class InlineVariableTool : CodeActionMutationToolHandler<InlineVariableRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineTemporary.CSharpInlineTemporaryCodeRefactoringProvider";
    private const string Title = "Inline temporary variable";
    private const string EquivalenceKey = "Inline_temporary_variable";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "inline-variable",
        Title = "Inline Variable",
        Description = "Inlines a local variable through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new InlineVariableTool());
    }

    protected override async ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(InlineVariableRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (!request.RemoveDeclaration)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("UnsupportedOption", "The removeDeclaration option is not supported by the current Roslyn inline-variable backend.");
        }

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<MutationProposal>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not ILocalSymbol localSymbol)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("SymbolNotSupported", "The selected symbol is not a local variable.", RequiredAction.ResolveTargetAgain);
        }

        var sourceLocation = localSymbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is null)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("SymbolNotSupported", "The selected symbol does not resolve to a source location.", RequiredAction.ResolveTargetAgain);
        }

        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(sourceLocation);
        var locationSelector = ToolExecutionHelpers.CreateLocationSelector(resolvedLocation);
        if (locationSelector is null)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("SymbolNotSupported", "The selected symbol does not resolve to a replayable source span.", RequiredAction.ResolveTargetAgain);
        }

        return await context.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = locationSelector,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = ProviderId,
            Title = Title,
            EquivalenceKey = EquivalenceKey,
        }, cancellationToken).ConfigureAwait(false);
    }
}
