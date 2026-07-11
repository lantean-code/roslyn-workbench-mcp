using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class TransactionCommitService : ITransactionCommitService
{
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly ISnapshotGuard _snapshotGuard;
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly IWorkspaceCommitWriter _commitWriter;
    private readonly IWorkspaceCommitPlanner _commitPlanner;
    private readonly IWorkspaceCommitLockManager _commitLockManager;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;

    public TransactionCommitService(
        IWorkspaceSessionStore sessionStore,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        ISnapshotGuard snapshotGuard,
        IWorkspaceOperationResultFactory resultFactory,
        ICommitRecoveryStore recoveryStore,
        IWorkspaceCommitWriter commitWriter,
        IWorkspaceCommitPlanner commitPlanner,
        IWorkspaceCommitLockManager commitLockManager,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher)
    {
        _sessionStore = sessionStore;
        _workspaceChangeDetector = workspaceChangeDetector;
        _workspaceStateTransitions = workspaceStateTransitions;
        _snapshotGuard = snapshotGuard;
        _resultFactory = resultFactory;
        _recoveryStore = recoveryStore;
        _commitWriter = commitWriter;
        _commitPlanner = commitPlanner;
        _commitLockManager = commitLockManager;
        _instanceStatusPublisher = instanceStatusPublisher;
    }

    public async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        WorkspaceSelection selection,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        cancellationToken.ThrowIfCancellationRequested();

        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        var transaction = session?.Transaction;
        if (session is null || transaction is null)
        {
            return _resultFactory.Rejected<TransactionCommitOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before committing changes.",
                RequiredAction.StartTransaction);
        }

        var context = CreateContext(session);
        var snapshotMismatch = _snapshotGuard.Validate(session, expectedSnapshot);
        if (snapshotMismatch is not null)
        {
            return _resultFactory.Conflict<TransactionCommitOutcome>(snapshotMismatch, context);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return _resultFactory.Conflict<TransactionCommitOutcome>(
                WorkspaceErrorCodes.TransactionConflicted,
                "Roll back the conflicted transaction before committing changes.",
                RequiredAction.RollbackTransaction,
                context);
        }

        if (transaction.CurrentRevision == 0)
        {
            return _resultFactory.NoChange(
                context,
                new TransactionCommitOutcome
                {
                    Committed = false,
                    Transaction = transaction.ToInfo(conflicted: false),
                });
        }

        if (_workspaceChangeDetector.HasChanged(session.InputManifest, cancellationToken))
        {
            session = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
            _sessionStore.ReplaceSession(session);
            transaction = session.Transaction
                ?? throw new InvalidOperationException("The conflicted session did not retain its transaction.");

            return _resultFactory.Conflict<TransactionCommitOutcome>(
                WorkspaceErrorCodes.TransactionConflicted,
                "The transaction conflicted with external workspace changes.",
                RequiredAction.RollbackTransaction,
                CreateContext(session));
        }

        var lockAcquisition = _commitLockManager.Acquire(session.Workspace.WorkspaceRoot);
        if (lockAcquisition.IsContended)
        {
            return _resultFactory.Rejected<TransactionCommitOutcome>(
                WorkspaceErrorCodes.WorkspaceBusy,
                "Another server instance is committing this workspace.",
                RequiredAction.Retry);
        }

        if (lockAcquisition.IsFailed)
        {
            return _resultFactory.Faulted<TransactionCommitOutcome>(
                "CommitLockFailed",
                lockAcquisition.ErrorMessage,
                RequiredAction.Retry,
                context);
        }

        using var commitLock = lockAcquisition.Lock;

        var commitId = Guid.NewGuid().ToString("n");
        WorkspaceCommitManifest? manifest = null;
        var applicationStarted = false;
        try
        {
            var plan = await _commitPlanner.CreateAsync(
                commitId,
                session.Workspace.LoadedPath,
                session.Workspace.WorkspaceRoot,
                transaction.BaselineSolution,
                transaction.CurrentSolution,
                cancellationToken).ConfigureAwait(false);
            manifest = plan.Manifest;
            await _instanceStatusPublisher.UpdateAsync(
                session.Workspace.WorkspaceId,
                session.State,
                transaction.CurrentRevision,
                commitId,
                "Staging").ConfigureAwait(false);
            await _recoveryStore.PersistPlanAsync(plan, cancellationToken).ConfigureAwait(false);
            await _commitWriter.RevalidateAsync(manifest, cancellationToken).ConfigureAwait(false);

            manifest = manifest with { State = RecoveryState.Applying };
            await _recoveryStore.WriteManifestAsync(manifest, cancellationToken).ConfigureAwait(false);
            await _instanceStatusPublisher.UpdateAsync(
                session.Workspace.WorkspaceId,
                session.State,
                transaction.CurrentRevision,
                commitId,
                "Applying").ConfigureAwait(false);

            applicationStarted = true;
            await _commitWriter.ApplyAsync(manifest).ConfigureAwait(false);
            var committedSession = session with
            {
                Transaction = null,
                CurrentSolution = transaction.CurrentSolution,
                InputManifest = _workspaceChangeDetector.BuildManifest(transaction.CurrentSolution, session.Workspace.LoadedPath),
                State = _workspaceStateTransitions.Fire(session.State, WorkspaceTrigger.TransactionCommitted),
            };

            manifest = manifest with { State = RecoveryState.Committed };
            await _recoveryStore.WriteManifestAsync(manifest, CancellationToken.None).ConfigureAwait(false);

            _sessionStore.ReplaceSessionAndSetTransactionOwner(committedSession, null);
            if (await _commitWriter.CompleteAsync(manifest).ConfigureAwait(false))
            {
                _recoveryStore.DeleteStatus(manifest.CommitId);
            }
            await _instanceStatusPublisher.UpdateAsync(
                committedSession.Workspace.WorkspaceId,
                committedSession.State,
                null,
                null,
                "Committed").ConfigureAwait(false);

            return _resultFactory.Succeeded(
                new TransactionCommitOutcome
                {
                    Committed = true,
                },
                CreateContext(committedSession));
        }
        catch (OperationCanceledException) when (!applicationStarted)
        {
            if (manifest is not null)
            {
                var state = await _commitWriter.RestoreAsync(manifest).ConfigureAwait(false);
                await TryWriteManifestAsync(manifest with { State = state }).ConfigureAwait(false);
                if (state == RecoveryState.Restored)
                {
                    _recoveryStore.DeleteStatus(manifest.CommitId);
                }
            }

            throw;
        }
        catch (Exception exception) when (IsRecoverableFileSystemException(exception))
        {
            var state = manifest is null
                ? RecoveryState.RecoveryIncomplete
                : await _commitWriter.RestoreAsync(manifest).ConfigureAwait(false);
            if (manifest is not null)
            {
                await _instanceStatusPublisher.UpdateAsync(
                    session.Workspace.WorkspaceId,
                    session.State,
                    transaction.CurrentRevision,
                    commitId,
                    "Restoring").ConfigureAwait(false);
                var recovered = manifest with { State = state, Message = exception.Message };
                await TryWriteManifestAsync(recovered).ConfigureAwait(false);
                if (state == RecoveryState.Restored)
                {
                    _recoveryStore.DeleteStatus(manifest.CommitId);
                }
                await _instanceStatusPublisher.UpdateAsync(
                    session.Workspace.WorkspaceId,
                    session.State,
                    transaction.CurrentRevision,
                    commitId,
                    state.ToString()).ConfigureAwait(false);
            }

            return _resultFactory.Faulted<TransactionCommitOutcome>(
                applicationStarted ? "CommitFailed" : "CommitPreparationFailed",
                applicationStarted
                    ? "The transaction commit failed and its changes were restored or retained for recovery."
                    : "The transaction commit could not update its recovery record and no workspace changes were applied.",
                !applicationStarted || state == RecoveryState.Restored ? RequiredAction.Retry : RequiredAction.ResolveRecovery,
                context);
        }
    }

    private async ValueTask TryWriteManifestAsync(WorkspaceCommitManifest manifest)
    {
        try
        {
            await _recoveryStore.WriteManifestAsync(manifest, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverableFileSystemException(exception))
        {
        }
    }

    private static bool IsRecoverableFileSystemException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    private static WorkspaceOperationContext CreateContext(WorkspaceSessionSnapshot session)
    {
        return new WorkspaceOperationContext
        {
            WorkspaceId = session.Workspace.WorkspaceId,
            WorkspaceEpoch = session.Workspace.WorkspaceEpoch,
            TransactionRevision = session.Transaction?.CurrentRevision,
        };
    }
}
