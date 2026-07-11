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

    public TransactionCommitService(
        IWorkspaceSessionStore sessionStore,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        ISnapshotGuard snapshotGuard,
        IWorkspaceOperationResultFactory resultFactory,
        ICommitRecoveryStore recoveryStore,
        IWorkspaceCommitWriter commitWriter)
    {
        _sessionStore = sessionStore;
        _workspaceChangeDetector = workspaceChangeDetector;
        _workspaceStateTransitions = workspaceStateTransitions;
        _snapshotGuard = snapshotGuard;
        _resultFactory = resultFactory;
        _recoveryStore = recoveryStore;
        _commitWriter = commitWriter;
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
            transaction = session.Transaction!;

            return _resultFactory.Conflict<TransactionCommitOutcome>(
                WorkspaceErrorCodes.TransactionConflicted,
                "The transaction conflicted with external workspace changes.",
                RequiredAction.RollbackTransaction,
                CreateContext(session));
        }

        var commitId = Guid.NewGuid().ToString("n");
        try
        {
            await WriteRecoveryStatusAsync(
                commitId,
                session.Workspace.LoadedPath,
                RecoveryState.Prepared,
                message: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverableFileSystemException(exception))
        {
            return _resultFactory.Faulted<TransactionCommitOutcome>(
                "CommitPreparationFailed",
                "The transaction commit could not create its recovery record and no workspace changes were applied.",
                RequiredAction.Retry,
                context);
        }

        var applicationStarted = false;
        try
        {
            await WriteRecoveryStatusAsync(
                commitId,
                session.Workspace.LoadedPath,
                RecoveryState.Applying,
                message: null,
                cancellationToken).ConfigureAwait(false);

            applicationStarted = true;
            await _commitWriter.ApplyAsync(transaction.BaselineSolution, transaction.CurrentSolution, cancellationToken);
            session.LoadedWorkspace.ApplyChanges(transaction.CurrentSolution);

            var committedSession = session with
            {
                Transaction = null,
                CurrentSolution = transaction.CurrentSolution,
                InputManifest = _workspaceChangeDetector.BuildManifest(transaction.CurrentSolution, session.Workspace.LoadedPath),
                State = _workspaceStateTransitions.Fire(session.State, WorkspaceTrigger.TransactionCommitted),
            };

            _sessionStore.ReplaceSessionAndSetTransactionOwner(committedSession, null);
            _recoveryStore.DeleteStatus(commitId);

            return _resultFactory.Succeeded(
                new TransactionCommitOutcome
                {
                    Committed = true,
                },
                CreateContext(committedSession));
        }
        catch (Exception exception) when (IsRecoverableFileSystemException(exception))
        {
            await TryWriteRecoveryIncompleteAsync(
                commitId,
                session.Workspace.LoadedPath,
                exception.Message,
                cancellationToken).ConfigureAwait(false);

            return _resultFactory.Faulted<TransactionCommitOutcome>(
                applicationStarted ? "CommitFailed" : "CommitPreparationFailed",
                applicationStarted
                    ? "The transaction commit could not be completed and may have partially applied workspace changes."
                    : "The transaction commit could not update its recovery record and no workspace changes were applied.",
                RequiredAction.ResolveRecovery,
                context);
        }
    }

    private async ValueTask WriteRecoveryStatusAsync(
        string commitId,
        string solutionPath,
        RecoveryState state,
        string? message,
        CancellationToken cancellationToken)
    {
        await _recoveryStore.WriteStatusAsync(new RecoveryStatus
        {
            CommitId = commitId,
            SolutionPath = solutionPath,
            State = state,
            Message = message,
        }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask TryWriteRecoveryIncompleteAsync(
        string commitId,
        string solutionPath,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteRecoveryStatusAsync(
                commitId,
                solutionPath,
                RecoveryState.RecoveryIncomplete,
                message,
                cancellationToken).ConfigureAwait(false);
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
