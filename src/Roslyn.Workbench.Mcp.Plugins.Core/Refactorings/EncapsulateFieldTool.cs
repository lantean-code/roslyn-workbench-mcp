using Roslyn.Workbench.Mcp.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class EncapsulateFieldTool : MutationToolHandler<EncapsulateFieldRequest, MutationProposal>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.EncapsulateField.EncapsulateFieldRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "encapsulate-field",
        Title = "Encapsulate Field",
        Description = "Encapsulates one field through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new EncapsulateFieldTool());
    }

    protected override async ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(EncapsulateFieldRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<MutationProposal>(request.Field, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not IFieldSymbol fieldSymbol)
        {
            return context.ToolExecutionServices.ResultShaper.Rejected<MutationProposal>("SymbolNotSupported", "The selected symbol is not a field.", RequiredAction.ResolveTargetAgain);
        }

        var sourceLocation = fieldSymbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is null)
        {
            return context.ToolExecutionServices.ResultShaper.Rejected<MutationProposal>("SymbolNotSupported", "The selected symbol does not resolve to a source location.", RequiredAction.ResolveTargetAgain);
        }

        var locationSelector = ToolExecutionHelpers.CreateLocationSelector(context.WorkspaceResolver.CreateResolvedLocation(sourceLocation));
        if (locationSelector is null)
        {
            return context.ToolExecutionServices.ResultShaper.Rejected<MutationProposal>("SymbolNotSupported", "The selected symbol does not resolve to a replayable source span.", RequiredAction.ResolveTargetAgain);
        }

        var (title, equivalenceKey) = request.UpdateReferences
            ? ($"Encapsulate field: '{fieldSymbol.Name}' (and use property)", $"Encapsulate_field_colon_0_and_use_property_{fieldSymbol.Name}")
            : ($"Encapsulate field: '{fieldSymbol.Name}' (but still use field)", $"Encapsulate_field_colon_0_but_still_use_field_{fieldSymbol.Name}");

        return await context.CodeActionService.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = locationSelector,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = ProviderId,
            Title = title,
            EquivalenceKey = equivalenceKey,
        }, context, cancellationToken).ConfigureAwait(false);
    }
}
