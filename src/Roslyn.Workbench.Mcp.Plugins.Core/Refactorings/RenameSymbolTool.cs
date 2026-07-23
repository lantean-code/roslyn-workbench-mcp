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
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<MutationCandidate>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (string.IsNullOrWhiteSpace(request.NewName))
        {
            return PluginExecutionResultFactory.Rejected<MutationCandidate>("InvalidRequest", "A newName value is required.");
        }

        var symbol = symbolResolution.Value;
        if (string.Equals(symbol.Name, request.NewName, StringComparison.Ordinal))
        {
            return PluginExecutionResult<MutationCandidate>.NoChange();
        }

        var options = new SymbolRenameOptions(
            RenameOverloads: request.RenameOverloads,
            RenameInStrings: request.RenameInStrings,
            RenameInComments: request.RenameInComments,
            RenameFile: request.RenameFile);

        var candidateSolution = await Renamer.RenameSymbolAsync(context.CurrentSolution, symbol, options, request.NewName, cancellationToken);
        if (ReferenceEquals(candidateSolution, context.CurrentSolution))
        {
            return PluginExecutionResult<MutationCandidate>.NoChange();
        }

        var candidate = new MutationCandidate
        {
            CandidateSolution = candidateSolution,
            Summary = $"Rename '{symbol.Name}' to '{request.NewName}'.",
        };

        return PluginExecutionResult<MutationCandidate>.Success(candidate);
    }
}
