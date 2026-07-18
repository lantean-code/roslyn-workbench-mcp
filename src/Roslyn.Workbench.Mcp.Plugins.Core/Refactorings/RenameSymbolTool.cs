namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

[RoslynTool("rename-symbol", "Rename Symbol", "Stages a symbol rename across the effective solution.", Destructive = true)]
internal sealed class RenameSymbolTool : MutationToolHandler<RenameSymbolRequest>
{
    protected override ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteCoreAsync(RenameSymbolRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return ExecuteRenameSymbolAsync(request, context, cancellationToken);
    }

    private static async ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteRenameSymbolAsync(RenameSymbolRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<MutationCandidate>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (string.IsNullOrWhiteSpace(request.NewName))
        {
            return ToolExecutionHelpers.Rejected<MutationCandidate>("InvalidRequest", "A newName value is required.");
        }

        var symbol = symbolResolution.Value;
        var options = new SymbolRenameOptions(request.RenameOverloads, false, false, request.RenameFile);
        var candidateSolution = await Renamer.RenameSymbolAsync(context.CurrentSolution, symbol, options, request.NewName, cancellationToken).ConfigureAwait(false);
        if (ReferenceEquals(candidateSolution, context.CurrentSolution))
        {
            return PluginExecutionResult<MutationCandidate>.NoChange();
        }

        return PluginExecutionResult<MutationCandidate>.Success(new MutationCandidate
        {
            CandidateSolution = candidateSolution,
            Summary = $"Rename '{symbol.Name}' to '{request.NewName}'.",
        });
    }
}
