namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceMutationCandidateProcessor : IWorkspaceMutationCandidateProcessor
{
    private readonly IAddedDocumentProjectContextPropagator _addedDocumentProjectContextPropagator;
    private readonly IWorkspaceMutationCandidateValidator _candidateValidator;
    private readonly ILinkedDocumentChangeMerger _linkedDocumentChangeMerger;

    public WorkspaceMutationCandidateProcessor(
        IAddedDocumentProjectContextPropagator addedDocumentProjectContextPropagator,
        IWorkspaceMutationCandidateValidator candidateValidator,
        ILinkedDocumentChangeMerger linkedDocumentChangeMerger)
    {
        _addedDocumentProjectContextPropagator = addedDocumentProjectContextPropagator;
        _candidateValidator = candidateValidator;
        _linkedDocumentChangeMerger = linkedDocumentChangeMerger;
    }

    public async ValueTask<WorkspaceMutationCandidateProcessingResult> ProcessAsync(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken)
    {
        var validationError = _candidateValidator.Validate(
            currentSolution,
            candidateSolution);

        if (validationError is not null)
        {
            return WorkspaceMutationCandidateProcessingResult.Failed(validationError);
        }

        var propagatedSolution = await _addedDocumentProjectContextPropagator.PropagateAsync(
            currentSolution,
            candidateSolution,
            cancellationToken);

        var mergeResult = await _linkedDocumentChangeMerger.MergeAsync(
            currentSolution,
            propagatedSolution,
            cancellationToken);

        if (!mergeResult.IsSucceeded)
        {
            return WorkspaceMutationCandidateProcessingResult.Failed(mergeResult.Error);
        }

        validationError = _candidateValidator.Validate(
            currentSolution,
            mergeResult.Solution);

        if (validationError is not null)
        {
            return WorkspaceMutationCandidateProcessingResult.Failed(validationError);
        }

        return WorkspaceMutationCandidateProcessingResult.Succeeded(mergeResult.Solution);
    }
}
