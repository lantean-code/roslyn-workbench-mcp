namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class MutationStagingService : IMutationStagingService
{
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceDiffBuilder _diffBuilder;
    private readonly IWorkspaceResolverFactory _resolverFactory;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;
    private readonly IWorkspaceMutationCandidateProcessor _candidateProcessor;
    private readonly IWorkspaceMutationCandidateIdentityService _candidateIdentityService;

    public MutationStagingService(
        IWorkspaceOperationResultFactory resultFactory,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceDiffBuilder diffBuilder,
        IWorkspaceResolverFactory resolverFactory,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher,
        IWorkspaceMutationCandidateProcessor candidateProcessor,
        IWorkspaceMutationCandidateIdentityService candidateIdentityService)
    {
        _resultFactory = resultFactory;
        _sessionStore = sessionStore;
        _diffBuilder = diffBuilder;
        _resolverFactory = resolverFactory;
        _instanceStatusPublisher = instanceStatusPublisher;
        _candidateProcessor = candidateProcessor;
        _candidateIdentityService = candidateIdentityService;
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
        if (transactionOwnerWorkspaceId is null)
        {
            return CreateTransactionRequiredResult();
        }

        var session = _sessionStore.ReadSession(transactionOwnerWorkspaceId.Value);
        var transaction = session?.Transaction;
        if (session is null || transaction is null)
        {
            return CreateTransactionRequiredResult();
        }

        var processingResult = await _candidateProcessor.ProcessAsync(
            session.CurrentSolution,
            candidate.CandidateSolution,
            session.Workspace.WorkspaceRoot,
            cancellationToken);

        if (!processingResult.IsSucceeded)
        {
            if (candidate.Precondition is not null)
            {
                return CreateMutationCandidateChangedResult(diagnostics, warnings);
            }

            return CreateValidationFailureResult(processingResult.Error, diagnostics, warnings);
        }

        var mergedCandidate = candidate with
        {
            CandidateSolution = processingResult.Solution,
        };

        var preconditionFailure = await ValidatePreconditionAsync(
            session.CurrentSolution,
            mergedCandidate,
            diagnostics,
            warnings,
            cancellationToken);

        if (preconditionFailure is not null)
        {
            return preconditionFailure;
        }

        var stagedMutation = await CreateStagedMutationAsync(
            operationName,
            mergedCandidate,
            session,
            transaction,
            cancellationToken);

        _sessionStore.ReplaceSessionAfterStaging(
            stagedMutation.Session,
            stagedMutation.DiscardedSnapshotIds);

        await PublishStatusAsync(stagedMutation);

        return CreateSuccessResult(operationName, mergedCandidate, stagedMutation, diagnostics, warnings);
    }

    private async ValueTask<WorkspaceOperationResult<MutationStagingOutcome>?> ValidatePreconditionAsync(
        Solution currentSolution,
        WorkspaceMutationCandidate candidate,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        if (candidate.Precondition is null)
        {
            return null;
        }

        var identity = await _candidateIdentityService.CreateAsync(
            currentSolution,
            candidate.CandidateSolution,
            cancellationToken);

        if (_candidateIdentityService.MatchesPrecondition(candidate.Precondition, identity))
        {
            return null;
        }

        return CreateMutationCandidateChangedResult(diagnostics, warnings);
    }

    private WorkspaceOperationResult<MutationStagingOutcome> CreateMutationCandidateChangedResult(
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings)
    {
        return _resultFactory.Rejected<MutationStagingOutcome>(
            WorkspaceErrorCodes.MutationCandidateChanged,
            "The mutation candidate no longer matches the previously prepared operation.",
            RequiredAction.ResolveTargetAgain,
            diagnostics: diagnostics,
            warnings: warnings);
    }

    private async ValueTask<StagedMutation> CreateStagedMutationAsync(
        string operationName,
        WorkspaceMutationCandidate candidate,
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        CancellationToken cancellationToken)
    {
        var candidateResolver = _resolverFactory.Create(
            candidate.CandidateSolution,
            session.Workspace,
            transaction.CurrentRevision + 1);

        var changes = await _diffBuilder.CreateChangeSummaryAsync(
            transaction.BaselineSolution,
            candidate.CandidateSolution,
            candidateResolver,
            cancellationToken);

        var revision = CreateRevision(operationName, candidate, changes);
        var appendResult = transaction.Append(revision);
        var updatedTransaction = appendResult.Transaction;
        var snapshotIdentity = WorkspaceSnapshotIdentity.Create(
            session.Workspace,
            session.CommittedSnapshotId,
            updatedTransaction);

        var updatedSession = session with
        {
            Transaction = updatedTransaction,
            CurrentSolution = updatedTransaction.CurrentSolution,
            CurrentSnapshotIdentity = snapshotIdentity,
        };

        return new StagedMutation
        {
            Session = updatedSession,
            Transaction = updatedTransaction,
            Revision = revision,
            Changes = changes,
            DiscardedSnapshotIds = appendResult.DiscardedSnapshotIds,
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
        var outcome = CreateOutcome(operationName, candidate, stagedMutation);
        var combinedWarnings = warnings.Concat(candidate.Warnings).ToArray();
        return _resultFactory.Succeeded(
            outcome,
            diagnostics: diagnostics,
            warnings: combinedWarnings);
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

    private WorkspaceTransactionRevision CreateRevision(
        string operationName,
        WorkspaceMutationCandidate candidate,
        ChangeSummary stagedChanges)
    {
        var preview = new MutationPreview
        {
            Summary = candidate.Summary,
        };

        return new WorkspaceTransactionRevision
        {
            SnapshotId = _sessionStore.AllocateWorkspaceSnapshotId(),
            Solution = candidate.CandidateSolution,
            Changes = stagedChanges,
            Operation = operationName,
            Summary = candidate.Summary,
            Preview = preview,
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
