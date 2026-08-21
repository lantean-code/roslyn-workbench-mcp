namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceMutationCandidateProcessor : IWorkspaceMutationCandidateProcessor
{
    private readonly IAddedDocumentProjectContextPropagator _addedDocumentProjectContextPropagator;
    private readonly IWorkspaceMutationCandidateValidator _candidateValidator;
    private readonly ILinkedDocumentChangeMerger _linkedDocumentChangeMerger;
    private readonly IRelocatedDocumentProjectContextPropagator _relocatedDocumentProjectContextPropagator;
    private readonly IRemovedDocumentProjectContextPropagator _removedDocumentProjectContextPropagator;

    public WorkspaceMutationCandidateProcessor(
        IAddedDocumentProjectContextPropagator addedDocumentProjectContextPropagator,
        IWorkspaceMutationCandidateValidator candidateValidator,
        ILinkedDocumentChangeMerger linkedDocumentChangeMerger,
        IRelocatedDocumentProjectContextPropagator relocatedDocumentProjectContextPropagator,
        IRemovedDocumentProjectContextPropagator removedDocumentProjectContextPropagator)
    {
        _addedDocumentProjectContextPropagator = addedDocumentProjectContextPropagator;
        _candidateValidator = candidateValidator;
        _linkedDocumentChangeMerger = linkedDocumentChangeMerger;
        _relocatedDocumentProjectContextPropagator = relocatedDocumentProjectContextPropagator;
        _removedDocumentProjectContextPropagator = removedDocumentProjectContextPropagator;
    }

    public async ValueTask<WorkspaceMutationCandidateProcessingResult> ProcessAsync(
        Solution currentSolution,
        Solution candidateSolution,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        var validation = _candidateValidator.Validate(
            currentSolution,
            candidateSolution,
            workspaceRoot);

        if (!validation.IsValid)
        {
            return WorkspaceMutationCandidateProcessingResult.Failed(validation.Error);
        }

        var propagatedSolution = await _addedDocumentProjectContextPropagator.PropagateAsync(
            currentSolution,
            candidateSolution,
            cancellationToken);

        propagatedSolution = _removedDocumentProjectContextPropagator.Propagate(
            currentSolution,
            propagatedSolution,
            cancellationToken);

        propagatedSolution = _relocatedDocumentProjectContextPropagator.Propagate(
            currentSolution,
            propagatedSolution,
            cancellationToken);

        var mergeResult = await _linkedDocumentChangeMerger.MergeAsync(
            currentSolution,
            propagatedSolution,
            cancellationToken);

        if (!mergeResult.IsSucceeded)
        {
            return WorkspaceMutationCandidateProcessingResult.Failed(mergeResult.Error);
        }

        validation = _candidateValidator.Validate(
            currentSolution,
            mergeResult.Solution,
            workspaceRoot);

        if (!validation.IsValid)
        {
            return WorkspaceMutationCandidateProcessingResult.Failed(validation.Error);
        }

        return WorkspaceMutationCandidateProcessingResult.Succeeded(mergeResult.Solution);
    }
}
