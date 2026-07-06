using System.Text;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class TransactionCommitService : ITransactionCommitService
{
    private readonly WorkspaceCoordinatorOptions _options;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly ISnapshotGuard _snapshotGuard;
    private readonly IWorkspaceOperationResultFactory _resultFactory;

    public TransactionCommitService(
        IOptions<WorkspaceCoordinatorOptions> options,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        ISnapshotGuard snapshotGuard,
        IWorkspaceOperationResultFactory resultFactory)
    {
        _options = options.Value;
        _sessionStore = sessionStore;
        _workspaceChangeDetector = workspaceChangeDetector;
        _workspaceStateTransitions = workspaceStateTransitions;
        _snapshotGuard = snapshotGuard;
        _resultFactory = resultFactory;
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
        CommitRecoveryStore.WriteStatus(_options.StateDirectory, new RecoveryStatus
        {
            CommitId = commitId,
            SolutionPath = session.Workspace.LoadedPath,
            State = RecoveryState.Prepared,
        });

        try
        {
            CommitRecoveryStore.WriteStatus(_options.StateDirectory, new RecoveryStatus
            {
                CommitId = commitId,
                SolutionPath = session.Workspace.LoadedPath,
                State = RecoveryState.Applying,
            });

            await ApplyCommittedSolutionAsync(transaction.BaselineSolution, transaction.CurrentSolution, cancellationToken);
            TryApplyWorkspaceChanges(session.LoadedWorkspace, transaction.CurrentSolution);

            var committedSession = session with
            {
                Transaction = null,
                CurrentSolution = transaction.CurrentSolution,
                InputManifest = _workspaceChangeDetector.BuildManifest(transaction.CurrentSolution, session.Workspace.LoadedPath),
                State = _workspaceStateTransitions.Fire(session.State, WorkspaceTrigger.TransactionCommitted),
            };

            _sessionStore.ReplaceSessionAndSetTransactionOwner(committedSession, null);
            CommitRecoveryStore.DeleteStatus(_options.StateDirectory, commitId);

            return _resultFactory.Succeeded(
                new TransactionCommitOutcome
                {
                    Committed = true,
                },
                CreateContext(committedSession));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            CommitRecoveryStore.WriteStatus(_options.StateDirectory, new RecoveryStatus
            {
                CommitId = commitId,
                SolutionPath = session.Workspace.LoadedPath,
                State = RecoveryState.RecoveryIncomplete,
                Message = exception.Message,
            });

            return _resultFactory.Faulted<TransactionCommitOutcome>(
                "CommitFailed",
                "The transaction commit could not be completed.",
                RequiredAction.ResolveRecovery,
                context);
        }
    }

    private static void TryApplyWorkspaceChanges(MSBuildWorkspace? workspace, Solution solution)
    {
        if (workspace is null)
        {
            return;
        }

        try
        {
            _ = workspace.TryApplyChanges(solution);
        }
        catch (NotSupportedException)
        {
        }
    }

    private static async ValueTask ApplyCommittedSolutionAsync(Solution baselineSolution, Solution currentSolution, CancellationToken cancellationToken)
    {
        var solutionChanges = currentSolution.GetChanges(baselineSolution);

        foreach (var projectChange in solutionChanges.GetProjectChanges())
        {
            foreach (var documentId in projectChange.GetChangedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = currentSolution.GetDocument(documentId);
                if (document?.FilePath is null)
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(document.FilePath)!);
                await WriteDocumentTextAsync(document, cancellationToken);
            }

            foreach (var documentId in projectChange.GetAddedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = currentSolution.GetDocument(documentId);
                if (document?.FilePath is null)
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(document.FilePath)!);
                await WriteDocumentTextAsync(document, cancellationToken);
            }

            foreach (var documentId in projectChange.GetRemovedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = baselineSolution.GetDocument(documentId);
                if (!string.IsNullOrWhiteSpace(document?.FilePath) && File.Exists(document.FilePath))
                {
                    File.Delete(document.FilePath);
                }
            }
        }
    }

    private static async ValueTask WriteDocumentTextAsync(Document document, CancellationToken cancellationToken)
    {
        var sourceText = await document.GetTextAsync(cancellationToken);
        await using var stream = File.Create(document.FilePath!);
        await using var writer = new StreamWriter(stream, sourceText.Encoding ?? Encoding.UTF8);
        sourceText.Write(writer, cancellationToken);
        await writer.FlushAsync(cancellationToken);
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
