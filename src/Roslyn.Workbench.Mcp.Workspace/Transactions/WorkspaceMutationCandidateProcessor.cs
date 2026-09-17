using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Normalises linked and project-context document changes before a candidate is staged.
/// </summary>
internal sealed class WorkspaceMutationCandidateProcessor : IWorkspaceMutationCandidateProcessor
{
    private readonly IAddedDocumentProjectContextPropagator _addedDocumentProjectContextPropagator;
    private readonly IWorkspaceMutationCandidateValidator _candidateValidator;
    private readonly ILinkedDocumentChangeMerger _linkedDocumentChangeMerger;
    private readonly IGeneratedSourceMutationInspector _generatedSourceMutationInspector;
    private readonly WorkspaceOptions _options;
    private readonly IRelocatedDocumentProjectContextPropagator _relocatedDocumentProjectContextPropagator;
    private readonly IRemovedDocumentProjectContextPropagator _removedDocumentProjectContextPropagator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceMutationCandidateProcessor"/> class.
    /// </summary>
    /// <param name="addedDocumentProjectContextPropagator">The component that propagates added document project context.</param>
    /// <param name="candidateValidator">The validator that checks a candidate solution before staging.</param>
    /// <param name="linkedDocumentChangeMerger">The component that reconciles edits to linked documents.</param>
    /// <param name="generatedSourceMutationInspector">The component that identifies generated-looking checked-in source changes.</param>
    /// <param name="options">The effective generated-source mutation policy.</param>
    /// <param name="relocatedDocumentProjectContextPropagator">The component that propagates relocated document project context.</param>
    /// <param name="removedDocumentProjectContextPropagator">The component that propagates removed document project context.</param>
    public WorkspaceMutationCandidateProcessor(
        IAddedDocumentProjectContextPropagator addedDocumentProjectContextPropagator,
        IWorkspaceMutationCandidateValidator candidateValidator,
        ILinkedDocumentChangeMerger linkedDocumentChangeMerger,
        IGeneratedSourceMutationInspector generatedSourceMutationInspector,
        IOptions<WorkspaceOptions> options,
        IRelocatedDocumentProjectContextPropagator relocatedDocumentProjectContextPropagator,
        IRemovedDocumentProjectContextPropagator removedDocumentProjectContextPropagator)
    {
        _addedDocumentProjectContextPropagator = addedDocumentProjectContextPropagator;
        _candidateValidator = candidateValidator;
        _linkedDocumentChangeMerger = linkedDocumentChangeMerger;
        _generatedSourceMutationInspector = generatedSourceMutationInspector;
        _options = options.Value;
        _relocatedDocumentProjectContextPropagator = relocatedDocumentProjectContextPropagator;
        _removedDocumentProjectContextPropagator = removedDocumentProjectContextPropagator;
    }

    /// <summary>
    /// Propagates project context, merges linked changes, and validates the resulting candidate.
    /// </summary>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="candidateSolution">The candidate solution containing the proposed changes.</param>
    /// <param name="workspaceRoot">The workspace root path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace mutation candidate processing result.</returns>
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

        var generatedSourcePaths = await _generatedSourceMutationInspector.FindGeneratedSourcePathsAsync(
            currentSolution,
            mergeResult.Solution,
            workspaceRoot,
            cancellationToken);

        if (_options.GeneratedSourcePolicy == GeneratedSourcePolicy.Deny && generatedSourcePaths.Count > 0)
        {
            var error = new WorkspaceOperationError
            {
                Code = WorkspaceErrorCodes.GeneratedSourceMutationDenied,
                Message = $"Mutation proposals must not alter generated-looking checked-in source under the configured deny policy. First matching path: '{generatedSourcePaths[0]}'. Configure a generated-source exception and restart the server only when that path is intentionally maintained.",
            };

            return WorkspaceMutationCandidateProcessingResult.Failed(error);
        }

        var warnings = GeneratedSourceMutationWarnings.Create(generatedSourcePaths);
        return WorkspaceMutationCandidateProcessingResult.Succeeded(mergeResult.Solution, warnings);
    }
}
