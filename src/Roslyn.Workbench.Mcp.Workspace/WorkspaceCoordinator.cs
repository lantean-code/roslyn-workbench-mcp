using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Contracts.Transactions;
using Roslyn.Workbench.Mcp.Plugins;
using Stateless;

namespace Roslyn.Workbench.Mcp.Workspace;

public sealed class WorkspaceCoordinator : IWorkspaceCoordinator
{
    private const string _workspaceBusyCode = "WorkspaceBusy";
    private const string _workspaceNotOpenCode = "WorkspaceNotOpen";
    private const string _workspaceAlreadyOpenCode = "WorkspaceAlreadyOpen";
    private const string _workspaceNotSupportedCode = "WorkspaceNotSupported";
    private const string _workspaceOutOfDateCode = "WorkspaceOutOfDate";
    private const string _workspaceLoadFailedCode = "WorkspaceLoadFailed";
    private const string _transactionRequiredCode = "NoActiveTransaction";
    private const string _transactionAlreadyActiveCode = "TransactionAlreadyActive";
    private const string _transactionConflictedCode = "TransactionConflicted";
    private const string _transactionHistoryUnavailableCode = "TransactionHistoryUnavailable";
    private const string _transactionCapacityCode = "RevisionCapacityReached";
    private const string _transactionSnapshotMismatchCode = "SnapshotMismatch";

    private readonly Lock _syncRoot;
    private readonly WorkspaceCoordinatorOptions _options;
    private readonly ICodeActionService _codeActionService;
    private readonly WorkspaceOperationGate _operationGate;
    private readonly StateMachine<WorkspaceLifecycleState, WorkspaceTrigger> _stateMachine;
    private WorkspaceSnapshot _snapshot;
    private long _nextWorkspaceEpoch;

    public WorkspaceCoordinator(WorkspaceCoordinatorOptions options)
    {
        _options = options;
        _codeActionService = options.CodeActionService ?? new UnavailableCodeActionService();
        _syncRoot = new Lock();
        _operationGate = new WorkspaceOperationGate(options.MaxConcurrentQueries);
        _snapshot = new WorkspaceSnapshot
        {
            State = WorkspaceLifecycleState.Unloaded,
        };
        _nextWorkspaceEpoch = 1;
        _stateMachine = WorkspaceStateMachine.Create(
            () => _snapshot.State,
            value => _snapshot = _snapshot with { State = value });
    }

    public ValueTask<ToolExecutionContextLease<IMutationContext>> CreateMutationContextAsync(RegisteredTool tool, CancellationToken cancellationToken)
    {
        _ = tool;
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _operationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IMutationContext>.Rejected(CreateBusyResult()));
        }

        var snapshot = ReadSnapshot();
        var rejection = ValidateMutationSnapshot(snapshot, cancellationToken);
        if (rejection is not null)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IMutationContext>.Rejected(rejection, CreateMutationContext(snapshot), lease));
        }

        var context = CreateMutationContext(snapshot);
        return ValueTask.FromResult(ToolExecutionContextLease<IMutationContext>.Acquired(context, lease));
    }

    public ValueTask<ToolExecutionContextLease<IQueryContext>> CreateQueryContextAsync(RegisteredTool tool, CancellationToken cancellationToken)
    {
        _ = tool;
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _operationGate.TryAcquireShared();
        if (lease is null)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Rejected(CreateBusyResult()));
        }

        var snapshot = ReadSnapshot();
        if (snapshot.State == WorkspaceLifecycleState.Unloaded)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Rejected(CreateWorkspaceRequiredResult(), lease: lease));
        }

        if (snapshot.State is WorkspaceLifecycleState.Ready or WorkspaceLifecycleState.TransactionActive && HasExternalChange(snapshot, cancellationToken))
        {
            MarkExternalChangeDetected();
            snapshot = ReadSnapshot();
        }

        if (snapshot.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Rejected(
                CreateWorkspaceOutOfDateResult(snapshot.Workspace?.WorkspaceEpoch),
                CreateQueryContext(snapshot),
                lease));
        }

        if (snapshot.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Rejected(
                CreateTransactionConflictedResult(snapshot),
                CreateQueryContext(snapshot),
                lease));
        }

        return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Acquired(CreateQueryContext(snapshot), lease));
    }

    public async ValueTask<ToolResult<WorkspaceCloseData>> CloseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _operationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return CreateRejectedCloseResult(CreateBusyResult());
        }

        await using var leaseScope = lease;
        var snapshot = ReadSnapshot();

        if (snapshot.State == WorkspaceLifecycleState.Unloaded || snapshot.Workspace is null)
        {
            return CreateRejectedCloseResult(CreateWorkspaceRequiredResult());
        }

        if (snapshot.State is WorkspaceLifecycleState.TransactionActive or WorkspaceLifecycleState.TransactionConflicted)
        {
            return CreateRejectedCloseResult(CreateCommitOrRollbackRequiredResult(snapshot));
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            snapshot.LoadedWorkspace?.Dispose();
            _stateMachine.Fire(WorkspaceTrigger.CloseSucceeded);
            _snapshot = new WorkspaceSnapshot
            {
                State = WorkspaceLifecycleState.Unloaded,
            };
        }

        return ToolResult<WorkspaceCloseData>.Succeeded(new WorkspaceCloseData
        {
            ClosedPath = snapshot.Workspace.LoadedPath,
        });
    }

    public async ValueTask<ToolResult<WorkspaceStatusData>> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _operationGate.TryAcquireShared();
        if (lease is null)
        {
            return ToolResult<WorkspaceStatusData>.Rejected(
                CreateError(_workspaceBusyCode, "The workspace is busy."),
                RequiredAction.Retry,
                workspaceEpoch: ReadSnapshot().Workspace?.WorkspaceEpoch);
        }

        await using var leaseScope = lease;
        var snapshot = ReadSnapshot();

        if (snapshot.State is WorkspaceLifecycleState.Ready or WorkspaceLifecycleState.TransactionActive && HasExternalChange(snapshot, cancellationToken))
        {
            MarkExternalChangeDetected();
            snapshot = ReadSnapshot();
        }

        return ToolResult<WorkspaceStatusData>.Succeeded(
            CreateStatusData(snapshot),
            workspaceEpoch: snapshot.Workspace?.WorkspaceEpoch,
            transactionRevision: snapshot.Transaction?.CurrentRevision);
    }

    public async ValueTask<ToolResult<WorkspaceOpenData>> OpenAsync(WorkspaceOpenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _operationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return ToolResult<WorkspaceOpenData>.Rejected(CreateError(_workspaceBusyCode, "The workspace is busy."), RequiredAction.Retry);
        }

        await using var _ = lease.ConfigureAwait(false);

        var currentSnapshot = ReadSnapshot();
        if (currentSnapshot.State != WorkspaceLifecycleState.Unloaded)
        {
            return ToolResult<WorkspaceOpenData>.Rejected(
                CreateError(_workspaceAlreadyOpenCode, "A workspace is already open."),
                workspaceEpoch: currentSnapshot.Workspace?.WorkspaceEpoch);
        }

        var normalizedPath = NormalizeOpenPath(request.Path);
        if (normalizedPath is null)
        {
            return ToolResult<WorkspaceOpenData>.Rejected(CreateError("WorkspacePathInvalid", "Workspace paths must be absolute .sln, .slnx, or .csproj files."));
        }

        if (CommitRecoveryStore.GetStatuses(_options.StateDirectory).Any(status =>
                string.Equals(Path.GetFullPath(status.SolutionPath), normalizedPath, StringComparison.Ordinal)
                && status.State is not RecoveryState.Committed and not RecoveryState.Restored))
        {
            return ToolResult<WorkspaceOpenData>.Rejected(
                CreateError("RecoveryPending", "Resolve unfinished recovery work before opening this workspace."),
                RequiredAction.ResolveRecovery);
        }

        if (string.Equals(Path.GetExtension(normalizedPath), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var preflight = InspectProjectCompatibility(normalizedPath);
            if (preflight.Diagnostics.Count > 0)
            {
                return ToolResult<WorkspaceOpenData>.Rejected(
                    CreateError(_workspaceLoadFailedCode, "The workspace could not be loaded."),
                    diagnostics: preflight.Diagnostics);
            }

            if (!preflight.IsSdkStyle)
            {
                return ToolResult<WorkspaceOpenData>.Rejected(CreateError(_workspaceNotSupportedCode, "Only SDK-style C# projects are supported."));
            }
        }

        var loadedWorkspace = await LoadWorkspaceAsync(normalizedPath, cancellationToken);
        if (loadedWorkspace.Solution is null || loadedWorkspace.Workspace is null)
        {
            return ToolResult<WorkspaceOpenData>.Rejected(
                CreateError(_workspaceLoadFailedCode, "The workspace could not be loaded."),
                diagnostics: loadedWorkspace.Diagnostics);
        }

        var epoch = Interlocked.Increment(ref _nextWorkspaceEpoch);
        var snapshot = CreateSnapshot(loadedWorkspace.Workspace, loadedWorkspace.Solution, normalizedPath, epoch, loadedWorkspace.Diagnostics);

        foreach (var project in snapshot.LoadedWorkspace!.CurrentSolution.Projects
                     .Where(static project => string.Equals(project.Language, LanguageNames.CSharp, StringComparison.Ordinal))
                     .Where(static project => !string.IsNullOrWhiteSpace(project.FilePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projectCompatibility = InspectProjectCompatibility(project.FilePath!);
            if (projectCompatibility.Diagnostics.Count > 0)
            {
                snapshot.LoadedWorkspace.Dispose();
                return ToolResult<WorkspaceOpenData>.Rejected(
                    CreateError(_workspaceLoadFailedCode, "The workspace could not be loaded."),
                    diagnostics: projectCompatibility.Diagnostics);
            }

            if (!projectCompatibility.IsSdkStyle)
            {
                snapshot.LoadedWorkspace.Dispose();
                return ToolResult<WorkspaceOpenData>.Rejected(CreateError(_workspaceNotSupportedCode, "Only SDK-style C# projects are supported."));
            }
        }

        lock (_syncRoot)
        {
            _snapshot = _snapshot with { State = WorkspaceLifecycleState.Unloaded };
            _snapshot = snapshot with { State = WorkspaceLifecycleState.Unloaded };
            _stateMachine.Fire(WorkspaceTrigger.OpenSucceeded);
        }

        return ToolResult<WorkspaceOpenData>.Succeeded(
            new WorkspaceOpenData
            {
                Workspace = snapshot.Workspace,
                ProjectCount = snapshot.ProjectCount ?? 0,
                DocumentCount = snapshot.DocumentCount ?? 0,
                LoadDiagnostics = snapshot.LoadDiagnostics,
            },
            workspaceEpoch: snapshot.Workspace?.WorkspaceEpoch);
    }

    public async ValueTask<ToolResult<WorkspaceReloadData>> ReloadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _operationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return ToolResult<WorkspaceReloadData>.Rejected(CreateError(_workspaceBusyCode, "The workspace is busy."), RequiredAction.Retry);
        }

        await using var leaseScope = lease;
        var currentSnapshot = ReadSnapshot();
        if (currentSnapshot.Workspace is null)
        {
            return ToolResult<WorkspaceReloadData>.Rejected(CreateWorkspaceRequiredError(), RequiredAction.OpenWorkspace);
        }

        if (currentSnapshot.State is WorkspaceLifecycleState.TransactionActive or WorkspaceLifecycleState.TransactionConflicted)
        {
            return ToolResult<WorkspaceReloadData>.Rejected(
                CreateError("WorkspaceReloadBlocked", "Commit or roll back the active transaction before reloading."),
                RequiredAction.CommitOrRollback,
                workspaceEpoch: currentSnapshot.Workspace.WorkspaceEpoch,
                transactionRevision: currentSnapshot.Transaction?.CurrentRevision);
        }

        if (currentSnapshot.State != WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return ToolResult<WorkspaceReloadData>.Rejected(
                CreateError("WorkspaceReloadNotRequired", "The workspace does not require reload."),
                workspaceEpoch: currentSnapshot.Workspace.WorkspaceEpoch);
        }

        if (string.Equals(Path.GetExtension(currentSnapshot.Workspace.LoadedPath), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var preflight = InspectProjectCompatibility(currentSnapshot.Workspace.LoadedPath);
            if (preflight.Diagnostics.Count > 0)
            {
                return ToolResult<WorkspaceReloadData>.Rejected(
                    CreateError(_workspaceLoadFailedCode, "The workspace could not be reloaded."),
                    workspaceEpoch: currentSnapshot.Workspace.WorkspaceEpoch,
                    diagnostics: preflight.Diagnostics);
            }

            if (!preflight.IsSdkStyle)
            {
                return ToolResult<WorkspaceReloadData>.Rejected(
                    CreateError(_workspaceNotSupportedCode, "Only SDK-style C# projects are supported."),
                    workspaceEpoch: currentSnapshot.Workspace.WorkspaceEpoch);
            }
        }

        var loadedWorkspace = await LoadWorkspaceAsync(currentSnapshot.Workspace.LoadedPath, cancellationToken);
        if (loadedWorkspace.Solution is null || loadedWorkspace.Workspace is null)
        {
            return ToolResult<WorkspaceReloadData>.Rejected(
                CreateError(_workspaceLoadFailedCode, "The workspace could not be reloaded."),
                workspaceEpoch: currentSnapshot.Workspace.WorkspaceEpoch,
                diagnostics: loadedWorkspace.Diagnostics);
        }

        var epoch = Interlocked.Increment(ref _nextWorkspaceEpoch);
        var snapshot = CreateSnapshot(loadedWorkspace.Workspace, loadedWorkspace.Solution, currentSnapshot.Workspace.LoadedPath, epoch, loadedWorkspace.Diagnostics);

        lock (_syncRoot)
        {
            currentSnapshot.LoadedWorkspace?.Dispose();
            _snapshot = snapshot with { State = WorkspaceLifecycleState.WorkspaceOutOfDate };
            _stateMachine.Fire(WorkspaceTrigger.ReloadSucceeded);
        }

        return ToolResult<WorkspaceReloadData>.Succeeded(
            new WorkspaceReloadData
            {
                Workspace = snapshot.Workspace,
                ProjectCount = snapshot.ProjectCount ?? 0,
                DocumentCount = snapshot.DocumentCount ?? 0,
                LoadDiagnostics = snapshot.LoadDiagnostics,
            },
            workspaceEpoch: snapshot.Workspace?.WorkspaceEpoch);
    }

    public async ValueTask<ToolResult<TransactionStartData>> StartTransactionAsync(TransactionStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _operationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return ToolResult<TransactionStartData>.Rejected(CreateError(_workspaceBusyCode, "The workspace is busy."), RequiredAction.Retry);
        }

        await using var leaseScope = lease;
        var snapshot = ReadSnapshot();
        if (snapshot.State == WorkspaceLifecycleState.Unloaded || snapshot.Workspace is null || snapshot.CurrentSolution is null)
        {
            return ToolResult<TransactionStartData>.Rejected(CreateWorkspaceRequiredError(), RequiredAction.OpenWorkspace);
        }

        if (snapshot.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return ToolResult<TransactionStartData>.Conflict(
                CreateError(_workspaceOutOfDateCode, "Reload the workspace before starting a transaction."),
                RequiredAction.ReloadWorkspace,
                workspaceEpoch: snapshot.Workspace.WorkspaceEpoch);
        }

        if (snapshot.Transaction is not null)
        {
            return ToolResult<TransactionStartData>.Rejected(
                CreateError(_transactionAlreadyActiveCode, "A transaction is already active."),
                RequiredAction.CommitOrRollback,
                workspaceEpoch: snapshot.Workspace.WorkspaceEpoch,
                transactionRevision: snapshot.Transaction.CurrentRevision);
        }

        var transaction = new WorkspaceTransaction
        {
            BaselineSolution = snapshot.CurrentSolution,
            CurrentRevision = 0,
            MaxRevisions = _options.MaxTransactionRevisions,
        };

        lock (_syncRoot)
        {
            _snapshot = _snapshot with
            {
                Transaction = transaction,
                CurrentSolution = transaction.CurrentSolution,
            };
            _stateMachine.Fire(WorkspaceTrigger.TransactionStarted);
        }

        return ToolResult<TransactionStartData>.Succeeded(
            new TransactionStartData
            {
                Transaction = transaction.ToInfo(conflicted: false),
            },
            workspaceEpoch: snapshot.Workspace.WorkspaceEpoch,
            transactionRevision: transaction.CurrentRevision);
    }

    public async ValueTask<ToolResult<TransactionPreviewData>> PreviewTransactionAsync(TransactionPreviewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _operationGate.TryAcquireShared();
        if (lease is null)
        {
            return ToolResult<TransactionPreviewData>.Rejected(CreateError(_workspaceBusyCode, "The workspace is busy."), RequiredAction.Retry);
        }

        await using var leaseScope = lease;
        var snapshot = ReadSnapshot();
        if (snapshot.Transaction is null || snapshot.Workspace is null)
        {
            return ToolResult<TransactionPreviewData>.Rejected(
                CreateError(_transactionRequiredCode, "Start a transaction before previewing changes."),
                RequiredAction.StartTransaction,
                workspaceEpoch: snapshot.Workspace?.WorkspaceEpoch);
        }

        var resolver = new WorkspaceResolver(snapshot.Transaction.CurrentSolution, snapshot.Workspace, snapshot.Transaction.CurrentRevision);
        var changes = await WorkspaceDiffBuilder.CreateChangeSummaryAsync(
            snapshot.Transaction.BaselineSolution,
            snapshot.Transaction.CurrentSolution,
            resolver,
            cancellationToken);
        var documents = changes.Added.Concat(changes.Modified).Concat(changes.Deleted).ToArray();
        DocumentDiff? diff = null;

        if (request.IncludeDiff && request.Document is not null)
        {
            var resolution = resolver.ResolveDocument(request.Document);
            if (resolution.Status == SelectorResolveStatus.Resolved)
            {
                var reference = resolver.CreateDocumentReference(resolution.Value!);
                diff = reference is null
                    ? null
                    : await WorkspaceDiffBuilder.CreateDocumentDiffAsync(
                        snapshot.Transaction.BaselineSolution,
                        snapshot.Transaction.CurrentSolution,
                        reference,
                        resolver,
                        request.ContextLines,
                        cancellationToken);
            }
        }

        return ToolResult<TransactionPreviewData>.Succeeded(
            new TransactionPreviewData
            {
                Transaction = snapshot.Transaction.ToInfo(snapshot.State == WorkspaceLifecycleState.TransactionConflicted),
                Documents = documents,
                Diff = diff,
            },
            workspaceEpoch: snapshot.Workspace.WorkspaceEpoch,
            transactionRevision: snapshot.Transaction.CurrentRevision);
    }

    public async ValueTask<ToolResult<TransactionHistoryData>> MoveTransactionHistoryAsync(TransactionHistoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _operationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return ToolResult<TransactionHistoryData>.Rejected(CreateError(_workspaceBusyCode, "The workspace is busy."), RequiredAction.Retry);
        }

        await using var leaseScope = lease;
        var snapshot = ReadSnapshot();
        var transaction = snapshot.Transaction;

        if (snapshot.Workspace is null || transaction is null)
        {
            return ToolResult<TransactionHistoryData>.Rejected(CreateError(_transactionRequiredCode, "Start a transaction before moving history."), RequiredAction.StartTransaction);
        }

        var snapshotMismatch = ValidateSnapshotPrecondition(snapshot, request.ExpectedSnapshot);
        if (snapshotMismatch is not null)
        {
            return ToolResult<TransactionHistoryData>.Conflict(
                snapshotMismatch.Error!,
                snapshotMismatch.RequiredAction,
                workspaceEpoch: snapshot.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision);
        }

        if (snapshot.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return ToolResult<TransactionHistoryData>.Conflict(
                CreateError(_transactionConflictedCode, "Roll back the conflicted transaction before changing history."),
                RequiredAction.RollbackTransaction,
                workspaceEpoch: snapshot.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision);
        }

        var nextRevision = request.Direction switch
        {
            TransactionHistoryDirection.Undo when transaction.CurrentRevision > 0 => transaction.CurrentRevision - 1,
            TransactionHistoryDirection.Redo when transaction.CurrentRevision < transaction.Revisions.Count => transaction.CurrentRevision + 1,
            _ => -1,
        };

        if (nextRevision < 0)
        {
            return ToolResult<TransactionHistoryData>.Rejected(
                CreateError(_transactionHistoryUnavailableCode, "The requested transaction history move is unavailable."),
                workspaceEpoch: snapshot.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision);
        }

        var updatedTransaction = transaction with
        {
            CurrentRevision = nextRevision,
        };

        lock (_syncRoot)
        {
            _snapshot = _snapshot with
            {
                Transaction = updatedTransaction,
                CurrentSolution = updatedTransaction.CurrentSolution,
            };
        }

        return ToolResult<TransactionHistoryData>.Succeeded(
            new TransactionHistoryData
            {
                Transaction = updatedTransaction.ToInfo(conflicted: false),
            },
            workspaceEpoch: snapshot.Workspace.WorkspaceEpoch,
            transactionRevision: updatedTransaction.CurrentRevision);
    }

    public async ValueTask<ToolResult<TransactionCommitData>> CommitTransactionAsync(TransactionCommitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _operationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return ToolResult<TransactionCommitData>.Rejected(CreateError(_workspaceBusyCode, "The workspace is busy."), RequiredAction.Retry);
        }

        await using var leaseScope = lease;
        var snapshot = ReadSnapshot();
        var transaction = snapshot.Transaction;

        if (snapshot.Workspace is null || transaction is null)
        {
            return ToolResult<TransactionCommitData>.Rejected(CreateError(_transactionRequiredCode, "Start a transaction before committing changes."), RequiredAction.StartTransaction);
        }

        var snapshotMismatch = ValidateSnapshotPrecondition(snapshot, request.ExpectedSnapshot);
        if (snapshotMismatch is not null)
        {
            return ToolResult<TransactionCommitData>.Conflict(
                snapshotMismatch.Error!,
                snapshotMismatch.RequiredAction,
                workspaceEpoch: snapshot.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision);
        }

        if (snapshot.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return ToolResult<TransactionCommitData>.Conflict(
                CreateError(_transactionConflictedCode, "Roll back the conflicted transaction before committing changes."),
                RequiredAction.RollbackTransaction,
                workspaceEpoch: snapshot.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision);
        }

        if (transaction.CurrentRevision == 0)
        {
            return ToolResult<TransactionCommitData>.NoChange(
                workspaceEpoch: snapshot.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision,
                data: new TransactionCommitData
                {
                    Committed = false,
                    Transaction = transaction.ToInfo(conflicted: false),
                });
        }

        if (HasExternalChange(snapshot, cancellationToken))
        {
            MarkExternalChangeDetected();
            snapshot = ReadSnapshot();
            transaction = snapshot.Transaction!;

            return ToolResult<TransactionCommitData>.Conflict(
                CreateError(_transactionConflictedCode, "The transaction conflicted with external workspace changes."),
                RequiredAction.RollbackTransaction,
                workspaceEpoch: snapshot.Workspace!.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision);
        }

        var commitId = Guid.NewGuid().ToString("n");
        CommitRecoveryStore.WriteStatus(_options.StateDirectory, new RecoveryStatus
        {
            CommitId = commitId,
            SolutionPath = snapshot.Workspace.LoadedPath,
            State = RecoveryState.Prepared,
        });

        try
        {
            CommitRecoveryStore.WriteStatus(_options.StateDirectory, new RecoveryStatus
            {
                CommitId = commitId,
                SolutionPath = snapshot.Workspace.LoadedPath,
                State = RecoveryState.Applying,
            });

            await ApplyCommittedSolutionAsync(transaction.BaselineSolution, transaction.CurrentSolution, cancellationToken);
            TryApplyWorkspaceChanges(snapshot.LoadedWorkspace, transaction.CurrentSolution);

            lock (_syncRoot)
            {
                _snapshot = _snapshot with
                {
                    Transaction = null,
                    CurrentSolution = transaction.CurrentSolution,
                    InputManifest = WorkspaceInputManifestBuilder.Build(transaction.CurrentSolution, snapshot.Workspace!.LoadedPath),
                };
                _stateMachine.Fire(WorkspaceTrigger.TransactionCommitted);
            }

            CommitRecoveryStore.DeleteStatus(_options.StateDirectory, commitId);

            return ToolResult<TransactionCommitData>.Succeeded(
                new TransactionCommitData
                {
                    Committed = true,
                },
                workspaceEpoch: snapshot.Workspace!.WorkspaceEpoch);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            CommitRecoveryStore.WriteStatus(_options.StateDirectory, new RecoveryStatus
            {
                CommitId = commitId,
                SolutionPath = snapshot.Workspace.LoadedPath,
                State = RecoveryState.RecoveryIncomplete,
                Message = exception.Message,
            });

            return ToolResult<TransactionCommitData>.Faulted(
                CreateError("CommitFailed", "The transaction commit could not be completed."),
                RequiredAction.ResolveRecovery,
                workspaceEpoch: snapshot.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision);
        }
    }

    public async ValueTask<ToolResult<TransactionRollbackData>> RollbackTransactionAsync(TransactionRollbackRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _operationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return ToolResult<TransactionRollbackData>.Rejected(CreateError(_workspaceBusyCode, "The workspace is busy."), RequiredAction.Retry);
        }

        await using var leaseScope = lease;
        var snapshot = ReadSnapshot();
        var transaction = snapshot.Transaction;

        if (snapshot.Workspace is null || transaction is null)
        {
            return ToolResult<TransactionRollbackData>.Rejected(CreateError(_transactionRequiredCode, "Start a transaction before rolling back changes."), RequiredAction.StartTransaction);
        }

        var rollbackState = snapshot.State == WorkspaceLifecycleState.TransactionConflicted
            ? TransactionRollbackState.WorkspaceOutOfDate
            : TransactionRollbackState.Ready;

        lock (_syncRoot)
        {
            _snapshot = _snapshot with
            {
                Transaction = null,
                CurrentSolution = transaction.BaselineSolution,
            };
            _stateMachine.Fire(snapshot.State == WorkspaceLifecycleState.TransactionConflicted
                ? WorkspaceTrigger.ConflictedRollbackCompleted
                : WorkspaceTrigger.TransactionRolledBack);
        }

        return ToolResult<TransactionRollbackData>.Succeeded(
            new TransactionRollbackData
            {
                State = rollbackState,
            },
            workspaceEpoch: snapshot.Workspace.WorkspaceEpoch);
    }

    private static ToolResult<WorkspaceCloseData> CreateRejectedCloseResult(PluginExecutionResultBox result)
    {
        return ToolResult<WorkspaceCloseData>.Rejected(result.Error!, result.RequiredAction);
    }

    private static ToolError CreateError(string code, string message)
    {
        return new ToolError
        {
            Code = code,
            Message = message,
        };
    }

    private static string? NormalizeOpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            return null;
        }

        var normalizedPath = Path.GetFullPath(path);
        var extension = Path.GetExtension(normalizedPath);
        return extension is ".sln" or ".slnx" or ".csproj" ? normalizedPath : null;
    }

    private WorkspaceQueryContext CreateQueryContext(WorkspaceSnapshot snapshot)
    {
        var resolver = new WorkspaceResolver(snapshot.CurrentSolution!, snapshot.Workspace, snapshot.Transaction?.CurrentRevision);
        return new WorkspaceQueryContext(
            snapshot.CurrentSolution!,
            snapshot.Workspace,
            snapshot.Transaction?.CurrentRevision,
            new ResultLimit
            {
                MaxResults = _options.DefaultMaxResults,
            },
            _options.MaxResponseBytes,
            resolver,
            _codeActionService);
    }

    private WorkspaceMutationContext CreateMutationContext(WorkspaceSnapshot snapshot)
    {
        var resolver = new WorkspaceResolver(snapshot.CurrentSolution!, snapshot.Workspace, snapshot.Transaction?.CurrentRevision);
        return new WorkspaceMutationContext(
            snapshot.CurrentSolution!,
            snapshot.Workspace,
            snapshot.Transaction?.CurrentRevision,
            new ResultLimit
            {
                MaxResults = _options.DefaultMaxResults,
            },
            resolver,
            _codeActionService,
            StageMutationAsync);
    }

    private WorkspaceStatusData CreateStatusData(WorkspaceSnapshot snapshot)
    {
        return new WorkspaceStatusData
        {
            State = snapshot.State,
            Workspace = snapshot.Workspace,
            ProjectCount = snapshot.ProjectCount,
            DocumentCount = snapshot.DocumentCount,
            LoadDiagnostics = snapshot.LoadDiagnostics,
            Transaction = snapshot.Transaction?.ToInfo(snapshot.State == WorkspaceLifecycleState.TransactionConflicted),
            ReloadRequired = snapshot.State == WorkspaceLifecycleState.WorkspaceOutOfDate,
        };
    }

    private WorkspaceSnapshot CreateSnapshot(MSBuildWorkspace workspace, Solution solution, string loadedPath, long workspaceEpoch, IReadOnlyList<DiagnosticInfo> diagnostics)
    {
        return new WorkspaceSnapshot
        {
            State = WorkspaceLifecycleState.Ready,
            Workspace = new WorkspaceIdentity
            {
                WorkspaceEpoch = workspaceEpoch,
                LoadedPath = loadedPath,
            },
            LoadedWorkspace = workspace,
            CurrentSolution = solution,
            Transaction = null,
            ProjectCount = solution.Projects.Count(),
            DocumentCount = solution.Projects.Sum(static project => project.Documents.Count()),
            LoadDiagnostics = diagnostics,
            InputManifest = WorkspaceInputManifestBuilder.Build(solution, loadedPath),
        };
    }

    private PluginExecutionResultBox CreateBusyResult()
    {
        return new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Rejected,
            Error = CreateError(_workspaceBusyCode, "The workspace is busy."),
            RequiredAction = RequiredAction.Retry,
        };
    }

    private PluginExecutionResultBox CreateNoActiveTransactionResult()
    {
        return new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Rejected,
            Error = CreateError(_transactionRequiredCode, "Start a transaction before invoking mutation tools."),
            RequiredAction = RequiredAction.StartTransaction,
        };
    }

    private PluginExecutionResultBox CreateWorkspaceRequiredResult()
    {
        return new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Rejected,
            Error = CreateWorkspaceRequiredError(),
            RequiredAction = RequiredAction.OpenWorkspace,
        };
    }

    private ToolError CreateWorkspaceRequiredError()
    {
        return CreateError(_workspaceNotOpenCode, "Open a workspace before invoking this tool.");
    }

    private PluginExecutionResultBox CreateWorkspaceOutOfDateResult(long? workspaceEpoch)
    {
        return new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Conflict,
            Error = CreateError(_workspaceOutOfDateCode, "Reload the workspace before invoking this tool."),
            RequiredAction = RequiredAction.ReloadWorkspace,
        };
    }

    private PluginExecutionResultBox CreateTransactionConflictedResult(WorkspaceSnapshot snapshot)
    {
        return new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Conflict,
            Error = CreateError(_transactionConflictedCode, "Roll back the conflicted transaction before invoking this tool."),
            RequiredAction = RequiredAction.RollbackTransaction,
        };
    }

    private PluginExecutionResultBox CreateCommitOrRollbackRequiredResult(WorkspaceSnapshot snapshot)
    {
        return new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Rejected,
            Error = CreateError("TransactionOpen", "Commit or roll back the active transaction before invoking this tool."),
            RequiredAction = RequiredAction.CommitOrRollback,
        };
    }

    private bool HasExternalChange(WorkspaceSnapshot snapshot, CancellationToken cancellationToken)
    {
        return WorkspaceInputManifestValidator.HasChanged(snapshot.InputManifest, cancellationToken);
    }

    private (bool IsSdkStyle, IReadOnlyList<DiagnosticInfo> Diagnostics) InspectProjectCompatibility(string projectPath)
    {
        return MsBuildProjectUtilities.InspectCompatibility(projectPath);
    }

    private async ValueTask<(MSBuildWorkspace? Workspace, Solution? Solution, IReadOnlyList<DiagnosticInfo> Diagnostics)> LoadWorkspaceAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workspace = _options.WorkspaceHostServices is null
            ? MSBuildWorkspace.Create()
            : MSBuildWorkspace.Create(_options.WorkspaceHostServices);
        var diagnostics = new List<DiagnosticInfo>();

        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            diagnostics.Add(new DiagnosticInfo
            {
                Id = "WorkspaceLoad",
                Severity = args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure
                    ? Contracts.Results.DiagnosticSeverity.Error
                    : Contracts.Results.DiagnosticSeverity.Warning,
                Message = args.Diagnostic.Message,
            });
        });

        try
        {
            var solution = string.Equals(Path.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase)
                ? (await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken)).Solution
                : await workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken);

            return (workspace, solution, diagnostics);
        }
        catch (OperationCanceledException)
        {
            workspace.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.Add(CreateLoadDiagnostic(exception.Message));
            workspace.Dispose();
            return (null, null, diagnostics);
        }
    }

    private void MarkExternalChangeDetected()
    {
        lock (_syncRoot)
        {
            if (_snapshot.State == WorkspaceLifecycleState.Ready)
            {
                _stateMachine.Fire(WorkspaceTrigger.ExternalChangeDetected);
            }
            else if (_snapshot.State == WorkspaceLifecycleState.TransactionActive)
            {
                _stateMachine.Fire(WorkspaceTrigger.TransactionConflictDetected);
            }
        }
    }

    private WorkspaceSnapshot ReadSnapshot()
    {
        lock (_syncRoot)
        {
            return _snapshot;
        }
    }

    private PluginExecutionResultBox? ValidateMutationSnapshot(WorkspaceSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.State == WorkspaceLifecycleState.Unloaded)
        {
            return CreateWorkspaceRequiredResult();
        }

        if (snapshot.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return CreateWorkspaceOutOfDateResult(snapshot.Workspace?.WorkspaceEpoch);
        }

        if (HasExternalChange(snapshot, cancellationToken))
        {
            MarkExternalChangeDetected();
            snapshot = ReadSnapshot();
        }

        if (snapshot.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return CreateTransactionConflictedResult(snapshot);
        }

        if (snapshot.Transaction is null)
        {
            return CreateNoActiveTransactionResult();
        }

        if (snapshot.Transaction.CurrentRevision >= snapshot.Transaction.MaxRevisions)
        {
            return new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = CreateError(_transactionCapacityCode, "Reduce transaction history before staging more changes."),
                RequiredAction = RequiredAction.ReduceTransactionHistory,
            };
        }

        return null;
    }

    private PluginExecutionResultBox? ValidateSnapshotPrecondition(WorkspaceSnapshot snapshot, SnapshotPrecondition? expectedSnapshot)
    {
        if (snapshot.Workspace is null || snapshot.Transaction is null)
        {
            return null;
        }

        if (expectedSnapshot is null)
        {
            return null;
        }

        if (expectedSnapshot.WorkspaceEpoch != snapshot.Workspace.WorkspaceEpoch
            || expectedSnapshot.TransactionRevision != snapshot.Transaction.CurrentRevision)
        {
            return new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Conflict,
                Error = CreateError(_transactionSnapshotMismatchCode, "The request snapshot does not match the current transaction snapshot."),
                RequiredAction = RequiredAction.ResolveTargetAgain,
            };
        }

        return null;
    }

    private async ValueTask<PluginExecutionResult<MutationData>> StageMutationAsync(
        RegisteredTool tool,
        MutationProposal proposal,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = ReadSnapshot();
        if (snapshot.Workspace is null || snapshot.Transaction is null || snapshot.CurrentSolution is null)
        {
            return PluginExecutionResult<MutationData>.Rejected(CreateError(_transactionRequiredCode, "Start a transaction before invoking mutation tools."), RequiredAction.StartTransaction);
        }

        var validationError = ValidateMutationProposal(snapshot.CurrentSolution, proposal);
        if (validationError is not null)
        {
            return PluginExecutionResult<MutationData>.Rejected(validationError.Value.error, validationError.Value.requiredAction, diagnostics, warnings);
        }

        var transaction = snapshot.Transaction;
        var stagedChanges = await WorkspaceDiffBuilder.CreateChangeSummaryAsync(
            transaction.BaselineSolution,
            proposal.CandidateSolution!,
            new WorkspaceResolver(proposal.CandidateSolution!, snapshot.Workspace, transaction.CurrentRevision + 1),
            cancellationToken);
        var retainedRevisions = transaction.Revisions.Take(transaction.CurrentRevision).ToArray();
        var revision = new WorkspaceTransactionRevision
        {
            Solution = proposal.CandidateSolution!,
            Changes = stagedChanges,
            Operation = tool.Metadata.Name,
            Summary = proposal.Summary,
            Preview = new MutationPreview
            {
                Summary = proposal.Summary,
            },
        };
        var updatedRevisions = retainedRevisions.Concat([revision]).ToArray();
        var updatedTransaction = transaction with
        {
            Revisions = updatedRevisions,
            CurrentRevision = updatedRevisions.Length,
        };

        lock (_syncRoot)
        {
            _snapshot = _snapshot with
            {
                Transaction = updatedTransaction,
                CurrentSolution = updatedTransaction.CurrentSolution,
            };
        }

        return PluginExecutionResult<MutationData>.Success(
            new MutationData
            {
                Operation = tool.Metadata.Name,
                Summary = proposal.Summary,
                Transaction = updatedTransaction.ToInfo(conflicted: false),
                Preview = revision.Preview,
            },
            stagedChanges,
            diagnostics,
            warnings.Concat(proposal.Warnings).ToArray());
    }

    private (ToolError error, RequiredAction? requiredAction)? ValidateMutationProposal(Solution currentSolution, MutationProposal proposal)
    {
        if (proposal.CandidateSolution is null)
        {
            return (CreateError("InvalidMutationProposal", "Mutation proposals must provide a candidate solution."), null);
        }

        if (!ReferenceEquals(proposal.CandidateSolution.Workspace, currentSolution.Workspace))
        {
            return (CreateError("InvalidMutationProposal", "Mutation proposals must belong to the current workspace."), null);
        }

        if (!string.Equals(proposal.CandidateSolution.FilePath, currentSolution.FilePath, StringComparison.Ordinal))
        {
            return (CreateError("InvalidMutationProposal", "Mutation proposals must target the current workspace solution."), null);
        }

        if (proposal.CandidateSolution.ProjectIds.Count != currentSolution.ProjectIds.Count)
        {
            return (CreateError("UnsupportedChange", "Mutation proposals must not add or remove projects."), null);
        }

        foreach (var projectId in currentSolution.ProjectIds)
        {
            var currentProject = currentSolution.GetProject(projectId);
            var candidateProject = proposal.CandidateSolution.GetProject(projectId);
            if (currentProject is null || candidateProject is null)
            {
                return (CreateError("UnsupportedChange", "Mutation proposals must not alter project identity."), null);
            }

            if (!string.Equals(candidateProject.FilePath, currentProject.FilePath, StringComparison.Ordinal)
                || !string.Equals(candidateProject.Name, currentProject.Name, StringComparison.Ordinal)
                || !string.Equals(candidateProject.AssemblyName, currentProject.AssemblyName, StringComparison.Ordinal)
                || !string.Equals(candidateProject.DefaultNamespace, currentProject.DefaultNamespace, StringComparison.Ordinal)
                || !Equals(candidateProject.CompilationOptions, currentProject.CompilationOptions)
                || !Equals(candidateProject.ParseOptions, currentProject.ParseOptions))
            {
                return (CreateError("UnsupportedChange", "Mutation proposals must not alter project identity or options."), null);
            }

            var projectChanges = candidateProject.GetChanges(currentProject);
            if (projectChanges.GetAddedMetadataReferences().Any()
                || projectChanges.GetRemovedMetadataReferences().Any()
                || projectChanges.GetAddedProjectReferences().Any()
                || projectChanges.GetRemovedProjectReferences().Any()
                || projectChanges.GetAddedAnalyzerReferences().Any()
                || projectChanges.GetRemovedAnalyzerReferences().Any()
                || projectChanges.GetAddedAdditionalDocuments().Any()
                || projectChanges.GetChangedAdditionalDocuments().Any()
                || projectChanges.GetRemovedAdditionalDocuments().Any()
                || projectChanges.GetAddedAnalyzerConfigDocuments().Any()
                || projectChanges.GetChangedAnalyzerConfigDocuments().Any()
                || projectChanges.GetRemovedAnalyzerConfigDocuments().Any())
            {
                return (CreateError("UnsupportedChange", "Mutation proposals must not alter project references or non-source documents."), null);
            }

            var textChangedDocuments = projectChanges.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true).ToHashSet();
            if (projectChanges.GetChangedDocuments().Except(textChangedDocuments).Any())
            {
                return (CreateError("UnsupportedChange", "Mutation proposals must not alter source document metadata."), null);
            }

            if (TryValidateSourceDocuments(currentProject, projectChanges.GetRemovedDocuments(), "deleted") is { } removedValidationError)
            {
                return removedValidationError;
            }

            if (TryValidateSourceDocuments(candidateProject, projectChanges.GetAddedDocuments(), "created") is { } addedValidationError)
            {
                return addedValidationError;
            }

            if (TryValidateSourceDocuments(candidateProject, textChangedDocuments, "changed") is { } changedValidationError)
            {
                return changedValidationError;
            }
        }

        return null;
    }

    private static (ToolError error, RequiredAction? requiredAction)? TryValidateSourceDocuments(
        Project project,
        IEnumerable<DocumentId> documentIds,
        string operation)
    {
        foreach (var documentId in documentIds)
        {
            var document = project.GetDocument(documentId);
            if (document is null
                || document.SourceCodeKind != SourceCodeKind.Regular
                || string.IsNullOrWhiteSpace(document.FilePath))
            {
                return (CreateError("UnsupportedChange", $"Mutation proposals must use regular source documents for {operation} files."), null);
            }

            var projectDirectory = Path.GetDirectoryName(project.FilePath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(projectDirectory)
                || !IsPathWithinDirectory(document.FilePath, projectDirectory))
            {
                return (CreateError("UnsupportedChange", "Mutation proposals must keep source files within the owning project directory."), null);
            }
        }

        return null;
    }

    private static bool IsPathWithinDirectory(string candidatePath, string directoryPath)
    {
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath));
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        var directoryPrefix = normalizedDirectory + Path.DirectorySeparatorChar;
        var altDirectoryPrefix = normalizedDirectory + Path.AltDirectorySeparatorChar;

        return normalizedCandidate.StartsWith(directoryPrefix, StringComparison.Ordinal)
            || normalizedCandidate.StartsWith(altDirectoryPrefix, StringComparison.Ordinal);
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

                var text = (await document.GetTextAsync(cancellationToken)).ToString();
                Directory.CreateDirectory(Path.GetDirectoryName(document.FilePath)!);
                File.WriteAllText(document.FilePath, text);
            }

            foreach (var documentId in projectChange.GetAddedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = currentSolution.GetDocument(documentId);
                if (document?.FilePath is null)
                {
                    continue;
                }

                var text = (await document.GetTextAsync(cancellationToken)).ToString();
                Directory.CreateDirectory(Path.GetDirectoryName(document.FilePath)!);
                File.WriteAllText(document.FilePath, text);
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

    private static DiagnosticInfo CreateLoadDiagnostic(string message)
    {
        return new DiagnosticInfo
        {
            Id = "WorkspaceLoad",
            Severity = Contracts.Results.DiagnosticSeverity.Error,
            Message = message,
        };
    }
}
