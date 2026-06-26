using Microsoft.CodeAnalysis.Rename;

using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class RenameSymbolTool : MutationToolHandler<RenameSymbolRequest, MutationProposal>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "rename-symbol",
        Title = "Rename Symbol",
        Description = "Stages a symbol rename across the effective solution.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new RenameSymbolTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(RenameSymbolRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return ExecuteRenameSymbolAsync(request, context, cancellationToken);
    }

    private static async ValueTask<PluginExecutionResult<MutationProposal>> ExecuteRenameSymbolAsync(RenameSymbolRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var symbolResolution = await ToolExecutionHelpers.ResolveSymbolAsync<MutationProposal>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (string.IsNullOrWhiteSpace(request.NewName))
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("InvalidRequest", "A newName value is required.");
        }

        var symbol = symbolResolution.Value;
        var options = new SymbolRenameOptions(request.RenameOverloads, false, false, request.RenameFile);
        var candidateSolution = await Renamer.RenameSymbolAsync(context.CurrentSolution, symbol, options, request.NewName, cancellationToken).ConfigureAwait(false);
        if (ReferenceEquals(candidateSolution, context.CurrentSolution))
        {
            return PluginExecutionResult<MutationProposal>.NoChange();
        }

        return PluginExecutionResult<MutationProposal>.Success(new MutationProposal
        {
            CandidateSolution = candidateSolution,
            Summary = $"Rename '{symbol.Name}' to '{request.NewName}'.",
        });
    }
}
