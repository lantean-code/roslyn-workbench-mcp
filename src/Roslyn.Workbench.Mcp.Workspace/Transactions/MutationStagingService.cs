namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class MutationStagingService : IMutationStagingService
{
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceDiffBuilder _diffBuilder;
    private readonly IWorkspaceResolverFactory _resolverFactory;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;
    private readonly IWorkspaceMutationCandidateValidator _candidateValidator;
    private readonly ILinkedDocumentChangeMerger _linkedDocumentChangeMerger;

    public MutationStagingService(
        IWorkspaceOperationResultFactory resultFactory,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceDiffBuilder diffBuilder,
        IWorkspaceResolverFactory resolverFactory,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher,
        IWorkspaceMutationCandidateValidator candidateValidator,
        ILinkedDocumentChangeMerger linkedDocumentChangeMerger)
    {
        _resultFactory = resultFactory;
        _sessionStore = sessionStore;
        _diffBuilder = diffBuilder;
        _resolverFactory = resolverFactory;
        _instanceStatusPublisher = instanceStatusPublisher;
        _candidateValidator = candidateValidator;
        _linkedDocumentChangeMerger = linkedDocumentChangeMerger;
    }

    public async ValueTask<WorkspaceOperationResult<MutationStagingOutcome>> StageAsync(
        string operationName,
        WorkspaceMutationCandidate candidate,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transactionOwnerWorkspaceId = _sessionStore.ReadSnapshot().TransactionOwnerWorkspaceId;
        if (string.IsNullOrWhiteSpace(transactionOwnerWorkspaceId))
        {
            return CreateTransactionRequiredResult();
        }

        var session = _sessionStore.ReadSession(transactionOwnerWorkspaceId);
        var transaction = session?.Transaction;
        if (session is null || transaction is null)
        {
            return CreateTransactionRequiredResult();
        }

        var validationError = _candidateValidator.Validate(session.CurrentSolution, candidate.CandidateSolution);
        if (validationError is not null)
        {
            return CreateValidationFailureResult(validationError, diagnostics, warnings);
        }

        var mergeResult = await _linkedDocumentChangeMerger.MergeAsync(
            session.CurrentSolution,
            candidate.CandidateSolution,
            cancellationToken);
        if (!mergeResult.IsSucceeded)
        {
            return CreateValidationFailureResult(mergeResult.Error, diagnostics, warnings);
        }

        var mergedCandidate = candidate with
        {
            CandidateSolution = mergeResult.Solution,
        };

        validationError = _candidateValidator.Validate(
            session.CurrentSolution,
            mergedCandidate.CandidateSolution);
        if (validationError is not null)
        {
            return CreateValidationFailureResult(validationError, diagnostics, warnings);
        }

        var stagedMutation = await CreateStagedMutationAsync(
            operationName,
            mergedCandidate,
            session,
            transaction,
            cancellationToken);
        _sessionStore.ReplaceSession(stagedMutation.Session);
        await PublishStatusAsync(stagedMutation);

        return CreateSuccessResult(operationName, mergedCandidate, stagedMutation, diagnostics, warnings);
    }

    private async ValueTask<StagedMutation> CreateStagedMutationAsync(
        string operationName,
        WorkspaceMutationCandidate candidate,
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        CancellationToken cancellationToken)
    {
        var changes = await _diffBuilder.CreateChangeSummaryAsync(
            transaction.BaselineSolution,
            candidate.CandidateSolution,
            _resolverFactory.Create(candidate.CandidateSolution, session.Workspace, transaction.CurrentRevision + 1),
            cancellationToken);
        var revision = CreateRevision(operationName, candidate, changes);
        var updatedTransaction = transaction.Append(revision);
        var updatedSession = session with
        {
            Transaction = updatedTransaction,
            CurrentSolution = updatedTransaction.CurrentSolution,
        };

        return new StagedMutation
        {
            Session = updatedSession,
            Transaction = updatedTransaction,
            Revision = revision,
            Changes = changes,
        };
    }

    private ValueTask PublishStatusAsync(StagedMutation stagedMutation)
    {
        return _instanceStatusPublisher.UpdateAsync(
            stagedMutation.Session.Workspace.WorkspaceId,
            stagedMutation.Session.State,
            stagedMutation.Transaction.CurrentRevision,
            null,
            null);
    }

    private WorkspaceOperationResult<MutationStagingOutcome> CreateSuccessResult(
        string operationName,
        WorkspaceMutationCandidate candidate,
        StagedMutation stagedMutation,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings)
    {
        return _resultFactory.Succeeded(
            CreateOutcome(operationName, candidate, stagedMutation),
            diagnostics: diagnostics,
            warnings: warnings.Concat(candidate.Warnings).ToArray());
    }

    private WorkspaceOperationResult<MutationStagingOutcome> CreateTransactionRequiredResult()
    {
        return _resultFactory.Rejected<MutationStagingOutcome>(
            WorkspaceErrorCodes.TransactionRequired,
            "Start a transaction before invoking mutation tools.",
            RequiredAction.StartTransaction);
    }

    private WorkspaceOperationResult<MutationStagingOutcome> CreateValidationFailureResult(
        WorkspaceOperationError validationError,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings)
    {
        return _resultFactory.Rejected<MutationStagingOutcome>(
            validationError,
            diagnostics: diagnostics,
            warnings: warnings);
    }

    private static WorkspaceTransactionRevision CreateRevision(
        string operationName,
        WorkspaceMutationCandidate candidate,
        ChangeSummary stagedChanges)
    {
        return new WorkspaceTransactionRevision
        {
            Solution = candidate.CandidateSolution,
            Changes = stagedChanges,
            Operation = operationName,
            Summary = candidate.Summary,
            Preview = new MutationPreview
            {
                Summary = candidate.Summary,
            },
        };
    }

    private static MutationStagingOutcome CreateOutcome(
        string operationName,
        WorkspaceMutationCandidate candidate,
        StagedMutation stagedMutation)
    {
        return new MutationStagingOutcome
        {
            Operation = operationName,
            Summary = candidate.Summary,
            Transaction = stagedMutation.Transaction.ToInfo(conflicted: false),
            Preview = stagedMutation.Revision.Preview,
            Changes = stagedMutation.Changes,
        };
    }
}
