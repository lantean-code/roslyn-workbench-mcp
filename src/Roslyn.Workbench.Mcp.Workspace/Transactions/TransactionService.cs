using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Implements transaction start, preview, history navigation, commit, and rollback workflows.
/// </summary>
internal sealed class TransactionService : ITransactionService
{
    private readonly WorkspaceOptions _options;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSessionAcquirer _sessionAcquirer;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly ISnapshotGuard _snapshotGuard;
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly ITransactionCommitService _transactionCommitService;
    private readonly IWorkspaceDiffBuilder _diffBuilder;
    private readonly ITransactionReviewBuilder? _reviewBuilder;
    private readonly ITransactionReviewIdentityService? _reviewIdentityService;
    private readonly ITransactionCompilerValidationService? _compilerValidationService;
    private readonly IWorkspaceResolverFactory _resolverFactory;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;

    private ITransactionReviewBuilder ReviewBuilder => _reviewBuilder
        ?? throw new InvalidOperationException("Transaction review services must be composed when receipt authorisation is required.");

    private ITransactionReviewIdentityService ReviewIdentityService => _reviewIdentityService
        ?? throw new InvalidOperationException("Transaction review services must be composed when receipt authorisation is required.");

    private ITransactionCompilerValidationService CompilerValidationService => _compilerValidationService
        ?? throw new InvalidOperationException("Compiler validation services must be composed when compiler validation is required.");

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionService"/> class.
    /// </summary>
    /// <param name="options">The configured transaction revision limit.</param>
    /// <param name="sessionStore">The store that publishes transactional session state.</param>
    /// <param name="sessionAcquirer">The component that acquires the workspace session used by a transaction operation.</param>
    /// <param name="workspaceStateTransitions">The coordinator that applies workspace lifecycle state changes.</param>
    /// <param name="snapshotGuard">The guard that rejects operations targeting a stale transaction snapshot.</param>
    /// <param name="resultFactory">The factory used to create protocol result payloads.</param>
    /// <param name="transactionCommitService">The service that provides transaction commit operations.</param>
    /// <param name="diffBuilder">The builder that calculates source differences between solution snapshots.</param>
    /// <param name="resolverFactory">The factory used to create the required resolver.</param>
    /// <param name="instanceStatusPublisher">The publisher that keeps the workspace instance record current.</param>
    /// <param name="compilerValidationService">The optional compiler-impact validator composed by Host policy.</param>
    public TransactionService(
        IOptions<WorkspaceOptions> options,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceSessionAcquirer sessionAcquirer,
        IWorkspaceStateTransitions workspaceStateTransitions,
        ISnapshotGuard snapshotGuard,
        IWorkspaceOperationResultFactory resultFactory,
        ITransactionCommitService transactionCommitService,
        IWorkspaceDiffBuilder diffBuilder,
        IWorkspaceResolverFactory resolverFactory,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher,
        ITransactionCompilerValidationService? compilerValidationService = null)
    {
        _options = options.Value;
        _sessionStore = sessionStore;
        _sessionAcquirer = sessionAcquirer;
        _workspaceStateTransitions = workspaceStateTransitions;
        _snapshotGuard = snapshotGuard;
        _resultFactory = resultFactory;
        _transactionCommitService = transactionCommitService;
        _diffBuilder = diffBuilder;
        _resolverFactory = resolverFactory;
        _instanceStatusPublisher = instanceStatusPublisher;
        _compilerValidationService = compilerValidationService;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionService"/> class.
    /// </summary>
    /// <param name="options">The configured transaction revision limit.</param>
    /// <param name="sessionStore">The store that publishes transactional session state.</param>
    /// <param name="sessionAcquirer">The component that acquires the workspace session used by a transaction operation.</param>
    /// <param name="workspaceStateTransitions">The coordinator that applies workspace lifecycle state changes.</param>
    /// <param name="snapshotGuard">The guard that rejects operations targeting a stale transaction snapshot.</param>
    /// <param name="resultFactory">The factory used to create protocol result payloads.</param>
    /// <param name="transactionCommitService">The service that provides transaction commit operations.</param>
    /// <param name="diffBuilder">The builder that calculates source differences between solution snapshots.</param>
    /// <param name="reviewBuilder">The builder that produces canonical transaction review receipts.</param>
    /// <param name="reviewIdentityService">The service that validates transaction review identity bindings.</param>
    /// <param name="resolverFactory">The factory used to create the required resolver.</param>
    /// <param name="instanceStatusPublisher">The publisher that keeps the workspace instance record current.</param>
    /// <param name="compilerValidationService">The optional compiler-impact validator composed by Host policy.</param>
    public TransactionService(
        IOptions<WorkspaceOptions> options,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceSessionAcquirer sessionAcquirer,
        IWorkspaceStateTransitions workspaceStateTransitions,
        ISnapshotGuard snapshotGuard,
        IWorkspaceOperationResultFactory resultFactory,
        ITransactionCommitService transactionCommitService,
        IWorkspaceDiffBuilder diffBuilder,
        ITransactionReviewBuilder reviewBuilder,
        ITransactionReviewIdentityService reviewIdentityService,
        IWorkspaceResolverFactory resolverFactory,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher,
        ITransactionCompilerValidationService? compilerValidationService = null)
        : this(
            options,
            sessionStore,
            sessionAcquirer,
            workspaceStateTransitions,
            snapshotGuard,
            resultFactory,
            transactionCommitService,
            diffBuilder,
            resolverFactory,
            instanceStatusPublisher,
            compilerValidationService)
    {
        _reviewBuilder = reviewBuilder;
        _reviewIdentityService = reviewIdentityService;
    }

    /// <summary>
    /// Starts the transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="alias">The alias used to address the registered item.</param>
    /// <param name="path">The path associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    public async ValueTask<WorkspaceOperationResult<TransactionStartOutcome>> StartAsync(Guid? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.SourceMutationEnabled)
        {
            return _resultFactory.Rejected<TransactionStartOutcome>(
                WorkspaceErrorCodes.SourceMutationDisabled,
                "Source mutation is disabled by Host operational policy.");
        }

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionStartOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;

        var context = WorkspaceOperationContextFactory.Create(session);
        if (session.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return _resultFactory.Conflict<TransactionStartOutcome>(
                WorkspaceErrorCodes.WorkspaceOutOfDate,
                "Reload the workspace before starting a transaction.",
                RequiredAction.ReloadWorkspace,
                context);
        }

        var ownerWorkspaceId = _sessionStore.ReadSnapshot().TransactionOwnerWorkspaceId;
        if (ownerWorkspaceId is not null && ownerWorkspaceId != acquisition.Selection.WorkspaceId)
        {
            var ownerSession = _sessionStore.ReadSession(ownerWorkspaceId.Value);
            return CreateTransactionOwnerResult<TransactionStartOutcome>(ownerSession, context);
        }

        if (session.Transaction is not null)
        {
            return _resultFactory.Rejected<TransactionStartOutcome>(
                WorkspaceErrorCodes.TransactionAlreadyActive,
                "A transaction is already active.",
                RequiredAction.CommitOrRollback,
                context);
        }

        var transaction = new WorkspaceTransaction
        {
            TransactionId = _sessionStore.AllocateWorkspaceTransactionId(),
            BaselineSnapshotId = session.CommittedSnapshotId,
            BaselineSolution = session.CurrentSolution,
            CurrentRevision = 0,
            MaxRevisions = _options.MaxTransactionRevisions,
        };

        var snapshotIdentity = WorkspaceSnapshotIdentity.Create(
            session.Workspace,
            session.CommittedSnapshotId,
            transaction);

        var updatedSession = session with
        {
            Transaction = transaction,
            CurrentSolution = transaction.CurrentSolution,
            State = _workspaceStateTransitions.Fire(session.State, WorkspaceTrigger.TransactionStarted),
            CurrentSnapshotIdentity = snapshotIdentity,
        };

        var admission = _sessionStore.TryStartTransaction(updatedSession);
        if (!admission.IsAdmitted)
        {
            var ownerSession = _sessionStore.ReadSession(admission.ExistingOwnerWorkspaceId.Value);
            return CreateTransactionOwnerResult<TransactionStartOutcome>(ownerSession, context);
        }

        await _instanceStatusPublisher.UpdateAsync(
            updatedSession.Workspace.WorkspaceId,
            updatedSession.State,
            transaction.CurrentRevision,
            null,
            null);

        var outcome = new TransactionStartOutcome
        {
            Transaction = transaction.ToInfo(conflicted: false),
        };

        var updatedContext = WorkspaceOperationContextFactory.Create(updatedSession);

        return _resultFactory.Succeeded(outcome, updatedContext);
    }

    /// <summary>
    /// Creates a bounded preview of the active transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="alias">The alias used to address the registered item.</param>
    /// <param name="path">The path associated with the operation.</param>
    /// <param name="document">The optional document whose detailed diff should be returned.</param>
    /// <param name="includeDiff">Whether the operation result should include a detailed source diff.</param>
    /// <param name="contextLines">The number of unchanged context lines to include around each difference.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    public async ValueTask<WorkspaceOperationResult<TransactionPreviewOutcome>> PreviewAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        DocumentSelector? document,
        bool includeDiff,
        int contextLines,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireShared(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionPreviewOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        if (session?.Transaction is null)
        {
            WorkspaceOperationContext? rejectionContext = null;
            if (session is not null)
            {
                rejectionContext = WorkspaceOperationContextFactory.Create(session);
            }

            return _resultFactory.Rejected<TransactionPreviewOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before previewing changes.",
                RequiredAction.StartTransaction,
                rejectionContext);
        }

        var snapshot = WorkspaceSnapshotPreconditionFactory.Create(
            session.CurrentSnapshotIdentity,
            session.Transaction.CurrentRevision);

        var resolver = _resolverFactory.Create(
            session.Transaction.CurrentSolution,
            session.Workspace,
            session.ProjectTargetFrameworks,
            snapshot);

        var context = WorkspaceOperationContextFactory.Create(session);
        DocumentReference? diffDocument = null;
        if (includeDiff)
        {
            if (document is null)
            {
                return _resultFactory.Rejected<TransactionPreviewOutcome>(
                    WorkspaceErrorCodes.InvalidRequest,
                    "A document selector is required when includeDiff is true.",
                    context: context);
            }

            var resolution = resolver.ResolveDocument(document);
            if (!resolution.IsResolved)
            {
                var (errorCode, message) = resolution.Status switch
                {
                    SelectorResolveStatus.Ambiguous => (
                        WorkspaceErrorCodes.DocumentAmbiguous,
                        "The document selector matched multiple results."),
                    SelectorResolveStatus.Invalid => (
                        WorkspaceErrorCodes.InvalidRequest,
                        "The document selector contains an invalid path."),
                    _ => (
                        WorkspaceErrorCodes.DocumentNotFound,
                        "The document selector did not match any result."),
                };

                return _resultFactory.Rejected<TransactionPreviewOutcome>(
                    errorCode,
                    message,
                    RequiredAction.ResolveTargetAgain,
                    context);
            }

            diffDocument = resolver.CreateDocumentReference(resolution.Value);
            if (diffDocument is null)
            {
                return _resultFactory.Rejected<TransactionPreviewOutcome>(
                    WorkspaceErrorCodes.DocumentNotFound,
                    "The resolved document cannot be represented within this workspace.",
                    RequiredAction.ResolveTargetAgain,
                    context);
            }
        }

        var changes = await _diffBuilder.CreateChangeSummaryAsync(
            session.Transaction.BaselineSolution,
            session.Transaction.CurrentSolution,
            resolver,
            cancellationToken);

        var documents = changes.Added.Concat(changes.Modified).Concat(changes.Deleted).ToArray();
        DocumentDiff? diff = null;

        if (diffDocument is not null)
        {
            diff = await _diffBuilder.CreateDocumentDiffAsync(
                session.Transaction.BaselineSolution,
                session.Transaction.CurrentSolution,
                diffDocument,
                resolver,
                contextLines,
                cancellationToken);
        }

        var outcome = new TransactionPreviewOutcome
        {
            Transaction = session.Transaction.ToInfo(session.State == WorkspaceLifecycleState.TransactionConflicted),
            Documents = documents,
            Diff = diff,
        };

        return _resultFactory.Succeeded(outcome, context);
    }

    /// <summary>
    /// Produces a canonical review receipt for the active transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="alias">The alias used to address the registered item.</param>
    /// <param name="path">The path associated with the operation.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="document">The optional document whose detailed diff should be returned.</param>
    /// <param name="includeDiff">Whether the operation result should include a detailed source diff.</param>
    /// <param name="contextLines">The number of unchanged context lines to include around each difference.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the canonical transaction review.</returns>
    public async ValueTask<WorkspaceOperationResult<TransactionReviewOutcome>> ReviewAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        SnapshotPrecondition? expectedSnapshot,
        DocumentSelector? document,
        bool includeDiff,
        int contextLines,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.ReceiptAuthorisationRequired)
        {
            return _resultFactory.Rejected<TransactionReviewOutcome>(
                WorkspaceErrorCodes.TransactionReviewUnavailable,
                "Transaction receipt review is not enabled by Host operational policy.");
        }

        var acquisition = _sessionAcquirer.AcquireShared(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionReviewOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        if (session?.Transaction is null)
        {
            WorkspaceOperationContext? rejectionContext = null;
            if (session is not null)
            {
                rejectionContext = WorkspaceOperationContextFactory.Create(session);
            }

            return _resultFactory.Rejected<TransactionReviewOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before reviewing changes.",
                RequiredAction.StartTransaction,
                rejectionContext);
        }

        var context = WorkspaceOperationContextFactory.Create(session);
        var snapshotValidation = _snapshotGuard.Validate(session, expectedSnapshot);
        if (!snapshotValidation.IsValid)
        {
            return _resultFactory.Conflict<TransactionReviewOutcome>(snapshotValidation.Error, context);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return _resultFactory.Conflict<TransactionReviewOutcome>(
                WorkspaceErrorCodes.TransactionConflicted,
                "Roll back the conflicted transaction before reviewing changes.",
                RequiredAction.RollbackTransaction,
                context);
        }

        if (session.Transaction.CurrentRevision == 0)
        {
            return _resultFactory.Rejected<TransactionReviewOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Stage at least one change before producing a transaction review receipt.",
                context: context);
        }

        TransactionCompilerValidationOutcome? compilerValidation = null;
        if (_options.CompilerValidationRequired)
        {
            compilerValidation = await CompilerValidationService.ValidateAsync(session, cancellationToken);
            if (!compilerValidation.Succeeded)
            {
                return CreateCompilerValidationFailure<TransactionReviewOutcome>(
                    compilerValidation,
                    context);
            }
        }

        var diffDocumentResult = ResolveReviewDiffDocument(session, document, includeDiff, context);
        if (diffDocumentResult.Error is not null)
        {
            return diffDocumentResult.Error;
        }

        var buildResult = await ReviewBuilder.CreateAsync(
            session,
            compilerValidation,
            diffDocumentResult.Document,
            contextLines,
            cancellationToken);

        if (!buildResult.IsSucceeded)
        {
            return _resultFactory.Conflict<TransactionReviewOutcome>(
                WorkspaceErrorCodes.TransactionConflicted,
                buildResult.ErrorMessage,
                RequiredAction.RollbackTransaction,
                context);
        }

        return _resultFactory.Succeeded(buildResult.Outcome, context);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceOperationResult<TransactionCompilerValidationOutcome>> ValidateCompilerImpactAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.CompilerValidationRequired)
        {
            return _resultFactory.Rejected<TransactionCompilerValidationOutcome>(
                WorkspaceErrorCodes.CompilerValidationUnavailable,
                "Compiler-impact validation is not enabled by Host policy.");
        }

        var acquisition = _sessionAcquirer.AcquireShared(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionCompilerValidationOutcome>(
                acquisition,
                acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        if (session?.Transaction is null)
        {
            WorkspaceOperationContext? rejectionContext = null;
            if (session is not null)
            {
                rejectionContext = WorkspaceOperationContextFactory.Create(session);
            }

            return _resultFactory.Rejected<TransactionCompilerValidationOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start and update a transaction before validating compiler impact.",
                RequiredAction.StartTransaction,
                rejectionContext);
        }

        var context = WorkspaceOperationContextFactory.Create(session);
        var snapshotValidation = _snapshotGuard.Validate(session, expectedSnapshot);
        if (!snapshotValidation.IsValid)
        {
            return _resultFactory.Conflict<TransactionCompilerValidationOutcome>(
                snapshotValidation.Error,
                context);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return _resultFactory.Conflict<TransactionCompilerValidationOutcome>(
                WorkspaceErrorCodes.TransactionConflicted,
                "Roll back the conflicted transaction before validating compiler impact.",
                RequiredAction.RollbackTransaction,
                context);
        }

        if (session.Transaction.CurrentRevision == 0)
        {
            var noChangeOutcome = new TransactionCompilerValidationOutcome
            {
                IsComplete = true,
                Succeeded = true,
                Transaction = session.Transaction.ToInfo(conflicted: false),
                BaselineErrorCount = 0,
                StagedErrorCount = 0,
                IntroducedErrorCount = 0,
                DurationMilliseconds = 0,
            };

            return _resultFactory.Succeeded(noChangeOutcome, context);
        }

        var outcome = await CompilerValidationService.ValidateAsync(session, cancellationToken);
        return _resultFactory.Succeeded(outcome, context);
    }

    /// <inheritdoc/>
    public ValueTask<WorkspaceOperationResult<TransactionReviewIdentity>> ValidateReceiptAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        SnapshotPrecondition? expectedSnapshot,
        TransactionReviewIdentity identity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.ReceiptAuthorisationRequired)
        {
            return ValueTask.FromResult(_resultFactory.Rejected<TransactionReviewIdentity>(
                WorkspaceErrorCodes.TransactionReviewUnavailable,
                "Transaction receipt validation is not enabled by Host operational policy."));
        }

        var acquisition = _sessionAcquirer.AcquireShared(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return ValueTask.FromResult(
                CreateAcquisitionFailureResult<TransactionReviewIdentity>(acquisition, acquisition.Error));
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        var transaction = session.Transaction;
        var context = WorkspaceOperationContextFactory.Create(session);
        if (transaction is null)
        {
            return ValueTask.FromResult(_resultFactory.Rejected<TransactionReviewIdentity>(
                WorkspaceErrorCodes.TransactionReceiptMismatch,
                "The reviewed transaction is no longer active. Run transaction-review again after starting or updating a transaction.",
                RequiredAction.ReviewTransaction,
                context));
        }

        var snapshotValidation = _snapshotGuard.Validate(session, expectedSnapshot);
        var identityMatches = ReviewIdentityService.IsBoundTo(identity, session, transaction);

        if (!snapshotValidation.IsValid || !identityMatches)
        {
            return ValueTask.FromResult(_resultFactory.Conflict<TransactionReviewIdentity>(
                WorkspaceErrorCodes.TransactionReceiptMismatch,
                "The transaction changed after this receipt was created. Run transaction-review again and approve the new receipt.",
                RequiredAction.ReviewTransaction,
                context));
        }

        return ValueTask.FromResult(_resultFactory.Succeeded(identity, context));
    }

    /// <summary>
    /// Moves the transaction backward or forward through revision history.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="alias">The alias used to address the registered item.</param>
    /// <param name="path">The path associated with the operation.</param>
    /// <param name="direction">The direction in which to move through transaction history.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    public async ValueTask<WorkspaceOperationResult<TransactionHistoryOutcome>> MoveHistoryAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        TransactionHistoryDirection direction,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionHistoryOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        var transaction = session.Transaction;
        if (transaction is null)
        {
            return _resultFactory.Rejected<TransactionHistoryOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before moving history.",
                RequiredAction.StartTransaction);
        }

        var context = WorkspaceOperationContextFactory.Create(session);
        var snapshotValidation = _snapshotGuard.Validate(session, expectedSnapshot);
        if (!snapshotValidation.IsValid)
        {
            return _resultFactory.Conflict<TransactionHistoryOutcome>(snapshotValidation.Error, context);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return _resultFactory.Conflict<TransactionHistoryOutcome>(
                WorkspaceErrorCodes.TransactionConflicted,
                "Roll back the conflicted transaction before changing history.",
                RequiredAction.RollbackTransaction,
                context);
        }

        var updatedTransaction = transaction.MoveHistory(direction);
        if (updatedTransaction is null)
        {
            return _resultFactory.Rejected<TransactionHistoryOutcome>(
                WorkspaceErrorCodes.TransactionHistoryUnavailable,
                "The requested transaction history move is unavailable.",
                context: context);
        }

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

        _sessionStore.ReplaceSession(updatedSession);
        await _instanceStatusPublisher.UpdateAsync(
            updatedSession.Workspace.WorkspaceId,
            updatedSession.State,
            updatedTransaction.CurrentRevision,
            null,
            null);

        var outcome = new TransactionHistoryOutcome
        {
            Transaction = updatedTransaction.ToInfo(conflicted: false),
        };

        var updatedContext = WorkspaceOperationContextFactory.Create(updatedSession);

        return _resultFactory.Succeeded(outcome, updatedContext);
    }

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="alias">The alias used to address the registered item.</param>
    /// <param name="path">The path associated with the operation.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    public ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken)
    {
        return CommitAsync(
            workspaceId,
            alias,
            path,
            expectedSnapshot,
            receiptAuthorisation: null,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        SnapshotPrecondition? expectedSnapshot,
        TransactionReceiptAuthorisation? receiptAuthorisation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionCommitOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        return await _transactionCommitService.CommitAsync(
            acquisition.Selection,
            expectedSnapshot,
            receiptAuthorisation,
            cancellationToken);
    }

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="alias">The alias used to address the registered item.</param>
    /// <param name="path">The path associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    public async ValueTask<WorkspaceOperationResult<TransactionRollbackOutcome>> RollbackAsync(Guid? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionRollbackOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        var transaction = session.Transaction;
        if (transaction is null)
        {
            return _resultFactory.Rejected<TransactionRollbackOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before rolling back changes.",
                RequiredAction.StartTransaction);
        }

        var rollbackState = TransactionRollbackState.Ready;
        var rollbackTrigger = WorkspaceTrigger.TransactionRolledBack;
        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            rollbackState = TransactionRollbackState.WorkspaceOutOfDate;
            rollbackTrigger = WorkspaceTrigger.ConflictedRollbackCompleted;
        }

        var snapshotIdentity = WorkspaceSnapshotIdentity.Create(
            session.Workspace,
            session.CommittedSnapshotId,
            transaction: null);

        var updatedSession = session with
        {
            Transaction = null,
            CurrentSolution = transaction.BaselineSolution,
            State = _workspaceStateTransitions.Fire(session.State, rollbackTrigger),
            CurrentSnapshotIdentity = snapshotIdentity,
        };

        var completion = _sessionStore.TryCompleteTransaction(updatedSession);
        if (!completion.IsCompleted)
        {
            var context = WorkspaceOperationContextFactory.Create(session);
            return _resultFactory.Faulted<TransactionRollbackOutcome>(
                "TransactionOwnershipChanged",
                completion.Failure.Message,
                context: context);
        }

        await _instanceStatusPublisher.UpdateAsync(
            updatedSession.Workspace.WorkspaceId,
            updatedSession.State,
            null,
            null,
            null);

        var outcome = new TransactionRollbackOutcome
        {
            State = rollbackState,
        };

        var updatedContext = WorkspaceOperationContextFactory.Create(updatedSession);

        return _resultFactory.Succeeded(outcome, updatedContext);
    }

    private ReviewDiffDocumentResolution ResolveReviewDiffDocument(
        WorkspaceSessionSnapshot session,
        DocumentSelector? document,
        bool includeDiff,
        WorkspaceOperationContext context)
    {
        if (!includeDiff)
        {
            return ReviewDiffDocumentResolution.Succeeded(document: null);
        }

        if (document is null)
        {
            var error = _resultFactory.Rejected<TransactionReviewOutcome>(
                WorkspaceErrorCodes.InvalidRequest,
                "A document selector is required when includeDiff is true.",
                context: context);

            return ReviewDiffDocumentResolution.Failed(error);
        }

        var transaction = session.Transaction
            ?? throw new InvalidOperationException("Review document resolution requires an active transaction.");

        var snapshot = WorkspaceSnapshotPreconditionFactory.Create(
            session.CurrentSnapshotIdentity,
            transaction.CurrentRevision);

        var resolver = _resolverFactory.Create(
            transaction.CurrentSolution,
            session.Workspace,
            session.ProjectTargetFrameworks,
            snapshot);

        var resolution = resolver.ResolveDocument(document);
        if (!resolution.IsResolved)
        {
            var (errorCode, message) = resolution.Status switch
            {
                SelectorResolveStatus.Ambiguous => (
                    WorkspaceErrorCodes.DocumentAmbiguous,
                    "The document selector matched multiple results."),
                SelectorResolveStatus.Invalid => (
                    WorkspaceErrorCodes.InvalidRequest,
                    "The document selector contains an invalid path."),
                _ => (
                    WorkspaceErrorCodes.DocumentNotFound,
                    "The document selector did not match any result."),
            };

            var error = _resultFactory.Rejected<TransactionReviewOutcome>(
                errorCode,
                message,
                RequiredAction.ResolveTargetAgain,
                context);

            return ReviewDiffDocumentResolution.Failed(error);
        }

        var diffDocument = resolver.CreateDocumentReference(resolution.Value);
        if (diffDocument is null)
        {
            var error = _resultFactory.Rejected<TransactionReviewOutcome>(
                WorkspaceErrorCodes.DocumentNotFound,
                "The resolved document cannot be represented within this workspace.",
                RequiredAction.ResolveTargetAgain,
                context);

            return ReviewDiffDocumentResolution.Failed(error);
        }

        return ReviewDiffDocumentResolution.Succeeded(diffDocument);
    }

    private static string GetWorkspaceDisplayName(WorkspaceSessionSnapshot? session)
    {
        if (session is null)
        {
            return "unknown";
        }

        return session.Workspace.Alias
            ?? session.Workspace.LoadedPath
            ?? session.Workspace.WorkspaceId.ToString();
    }

    private WorkspaceOperationResult<T> CreateTransactionOwnerResult<T>(
        WorkspaceSessionSnapshot? ownerSession,
        WorkspaceOperationContext context)
    {
        return _resultFactory.Rejected<T>(
            WorkspaceErrorCodes.TransactionOwner,
            $"Commit or roll back the transaction on workspace '{GetWorkspaceDisplayName(ownerSession)}' before starting a transaction on this workspace.",
            RequiredAction.CommitOrRollback,
            context);
    }

    private WorkspaceOperationResult<TOutcome> CreateCompilerValidationFailure<TOutcome>(
        TransactionCompilerValidationOutcome validation,
        WorkspaceOperationContext context)
    {
        var warnings = validation.Limitations
            .Select(static limitation => new WarningInfo
            {
                Code = WorkspaceErrorCodes.CompilerValidationIncomplete,
                Message = limitation,
            })
            .ToArray();

        if (!validation.IsComplete)
        {
            return _resultFactory.Rejected<TOutcome>(
                WorkspaceErrorCodes.CompilerValidationIncomplete,
                "Compiler validation could not evaluate every affected loaded project. No review receipt was created. Roll back the transaction, reload the Workspace, and start a new transaction before retrying.",
                validation.RecoveryAction,
                context,
                validation.IntroducedDiagnostics,
                warnings);
        }

        return _resultFactory.Rejected<TOutcome>(
            WorkspaceErrorCodes.NewCompilerErrors,
            $"The transaction introduced {validation.IntroducedErrorCount} compiler error(s). No review receipt was created and the transaction remains active.",
            context: context,
            diagnostics: validation.IntroducedDiagnostics,
            warnings: warnings);
    }

    private static WorkspaceSelector? CreateWorkspaceSelector(Guid? workspaceId, string? alias, string? path)
    {
        if (workspaceId is null && alias is null && path is null)
        {
            return null;
        }

        return new WorkspaceSelector
        {
            WorkspaceId = workspaceId,
            Alias = alias,
            Path = path,
        };
    }

    private WorkspaceOperationResult<TOutcome> CreateAcquisitionFailureResult<TOutcome>(
        WorkspaceSessionAcquisition acquisition,
        WorkspaceOperationError error)
    {
        WorkspaceOperationContext? context = null;
        if (acquisition.ContextSession is not null)
        {
            context = WorkspaceOperationContextFactory.Create(acquisition.ContextSession);
        }

        return _resultFactory.Rejected<TOutcome>(error, context);
    }

    private static void DisposeFailedAcquisition(WorkspaceSessionAcquisition acquisition)
    {
        acquisition.Lease?.Dispose();
    }
}
