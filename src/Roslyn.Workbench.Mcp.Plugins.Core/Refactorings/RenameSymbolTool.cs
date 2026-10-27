namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

/// <summary>
/// Stages a symbol rename across the effective solution.
/// </summary>
[RoslynTool("rename-symbol", "Rename Symbol", "Stages a Roslyn symbol rename and reference updates across the current transaction solution. Use this for a resolved symbol rename; use code-action discovery for provider-specific transformations. Requires an active transaction, symbol, newName and the current expectedSnapshot. renameOverloads, renameInStrings, renameInComments and renameFile all default to false; renameFile asks Roslyn to rename the containing file for type symbols and stages the resulting path change. An unchanged name produces no change. Successful staging returns mutation details and a new snapshot for subsequent requests. Inspect transaction-preview, or transaction-review in approval-required mode, then commit or roll back promptly. Source files are written only by transaction-commit; this tool does not create a Git commit.", Destructive = true)]
internal sealed class RenameSymbolTool : MutationToolHandler<RenameSymbolRequest>
{
    /// <inheritdoc/>
    protected override ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteCoreAsync(
        RenameSymbolRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        return ExecuteRenameSymbolAsync(request, context, cancellationToken);
    }

    private static async ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteRenameSymbolAsync(
        RenameSymbolRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<MutationCandidate>(
            request.Symbol,
            request.ExpectedSnapshot,
            context,
            cancellationToken);

        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (string.IsNullOrWhiteSpace(request.NewName))
        {
            return PluginExecutionResult.Rejected<MutationCandidate>(PluginErrorCodes.InvalidRequest, "A newName value is required.");
        }

        var symbol = symbolResolution.Value;
        if (string.Equals(symbol.Name, request.NewName, StringComparison.Ordinal))
        {
            return PluginExecutionResult.NoChange<MutationCandidate>();
        }

        var options = new SymbolRenameOptions(
            RenameOverloads: request.RenameOverloads,
            RenameInStrings: request.RenameInStrings,
            RenameInComments: request.RenameInComments,
            RenameFile: request.RenameFile);

        var candidateSolution = await Renamer.RenameSymbolAsync(
            context.CurrentSolution,
            symbol,
            options,
            request.NewName,
            cancellationToken);

        if (request.RenameFile)
        {
            candidateSolution = ApplyRenamedDocumentPaths(
                context.CurrentSolution,
                candidateSolution);
        }

        if (ReferenceEquals(candidateSolution, context.CurrentSolution))
        {
            return PluginExecutionResult.NoChange<MutationCandidate>();
        }

        var candidate = new MutationCandidate
        {
            CandidateSolution = candidateSolution,
            Summary = $"Rename '{symbol.Name}' to '{request.NewName}'.",
        };

        return PluginExecutionResult.Success(candidate);
    }

    private static Solution ApplyRenamedDocumentPaths(
        Solution currentSolution,
        Solution candidateSolution)
    {
        var result = candidateSolution;
        var solutionChanges = candidateSolution.GetChanges(currentSolution);
        foreach (var projectChanges in solutionChanges.GetProjectChanges())
        {
            foreach (var documentId in projectChanges.GetChangedDocuments())
            {
                var currentDocument = GetRequiredDocument(currentSolution, documentId);
                var candidateDocument = GetRequiredDocument(candidateSolution, documentId);
                if (string.Equals(currentDocument.Name, candidateDocument.Name, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(currentDocument.FilePath))
                {
                    continue;
                }

                var currentDirectory = Path.GetDirectoryName(currentDocument.FilePath);
                if (string.IsNullOrWhiteSpace(currentDirectory))
                {
                    continue;
                }

                var candidatePath = Path.Combine(currentDirectory, candidateDocument.Name);
                result = result.WithDocumentFilePath(documentId, candidatePath);
            }
        }

        return result;
    }

    private static Document GetRequiredDocument(Solution solution, DocumentId documentId)
    {
        return solution.GetDocument(documentId)
            ?? throw new InvalidOperationException(
                $"The document '{documentId}' is not present in the expected solution.");
    }
}
