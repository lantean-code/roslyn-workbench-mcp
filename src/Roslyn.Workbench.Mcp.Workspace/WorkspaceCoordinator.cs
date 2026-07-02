using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Contracts.Transactions;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

public sealed class WorkspaceCoordinator : IWorkspaceCoordinator
{
    private const string _workspaceBusyCode = "WorkspaceBusy";
    private const string _workspaceNotOpenCode = "WorkspaceNotOpen";
    private const string _workspaceAlreadyOpenCode = "WorkspaceAlreadyOpen";
    private const string _workspaceNotSupportedCode = "WorkspaceNotSupported";
    private const string _workspaceOutOfDateCode = "WorkspaceOutOfDate";
    private const string _workspaceLoadFailedCode = "WorkspaceLoadFailed";
    private const string _workspaceSelectorRequiredCode = "WorkspaceSelectorRequired";
    private const string _workspaceSelectorNotFoundCode = "WorkspaceSelectorNotFound";
    private const string _workspaceSelectorMismatchCode = "WorkspaceSelectorMismatch";
    private const string _workspaceCapacityCode = "WorkspaceCapacityReached";
    private const string _transactionRequiredCode = "NoActiveTransaction";
    private const string _transactionAlreadyActiveCode = "TransactionAlreadyActive";
    private const string _transactionConflictedCode = "TransactionConflicted";
    private const string _transactionOwnerCode = "TransactionOwnedByWorkspace";
    private const string _transactionHistoryUnavailableCode = "TransactionHistoryUnavailable";
    private const string _transactionCapacityCode = "RevisionCapacityReached";
    private const string _transactionSnapshotMismatchCode = "SnapshotMismatch";

    private readonly Lock _syncRoot;
    private readonly WorkspaceCoordinatorOptions _options;
    private readonly ICodeActionService _codeActionService;
    private WorkspaceHostSnapshot _snapshot;
    private long _nextWorkspaceEpoch;
    private long _nextWorkspaceId;

    public WorkspaceCoordinator(WorkspaceCoordinatorOptions options)
    {
        _options = options;
        _codeActionService = options.CodeActionService ?? new UnavailableCodeActionService();
        _syncRoot = new Lock();
        _snapshot = new WorkspaceHostSnapshot();
        _nextWorkspaceEpoch = 0;
        _nextWorkspaceId = 0;
    }

    public ValueTask<ToolExecutionContextLease<IMutationContext>> CreateMutationContextAsync(RegisteredTool tool, object request, CancellationToken cancellationToken)
    {
        _ = tool;
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var hostSnapshot = ReadSnapshot();
        var selectionError = TryResolveWorkspaceSelection(hostSnapshot, GetWorkspaceSelector(request), out var selection);
        if (selectionError is not null)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IMutationContext>.Rejected(selectionError));
        }

        var lease = selection!.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IMutationContext>.Rejected(CreateBusyResult(selection.Session)));
        }

        var session = ReadSession(selection.WorkspaceId);
        if (session is null)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IMutationContext>.Rejected(CreateWorkspaceRequiredResult(), lease: lease));
        }

        var rejection = ValidateMutationSession(selection.WorkspaceId, session, cancellationToken);
        if (rejection is not null)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IMutationContext>.Rejected(rejection, CreateMutationContext(session), lease));
        }

        return ValueTask.FromResult(ToolExecutionContextLease<IMutationContext>.Acquired(CreateMutationContext(session), lease));
    }

    public ValueTask<ToolExecutionContextLease<IQueryContext>> CreateQueryContextAsync(RegisteredTool tool, object request, CancellationToken cancellationToken)
    {
        _ = tool;
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var hostSnapshot = ReadSnapshot();
        var selectionError = TryResolveWorkspaceSelection(hostSnapshot, GetWorkspaceSelector(request), out var selection);
        if (selectionError is not null)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Rejected(selectionError));
        }

        var lease = selection!.Session.OperationGate.TryAcquireShared();
        if (lease is null)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Rejected(CreateBusyResult(selection.Session)));
        }

        var session = ReadSession(selection.WorkspaceId);
        if (session is null)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Rejected(CreateWorkspaceRequiredResult(), lease: lease));
        }

        if (session.State is WorkspaceLifecycleState.Ready or WorkspaceLifecycleState.TransactionActive && HasExternalChange(session, cancellationToken))
        {
            MarkExternalChangeDetected(selection.WorkspaceId);
            session = ReadSession(selection.WorkspaceId);
        }

        if (session is null)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Rejected(CreateWorkspaceRequiredResult(), lease: lease));
        }

        if (session.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Rejected(CreateWorkspaceOutOfDateResult(session), CreateQueryContext(session), lease));
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Rejected(CreateTransactionConflictedResult(session), CreateQueryContext(session), lease));
        }

        return ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Acquired(CreateQueryContext(session), lease));
    }

    public async ValueTask<ToolResult<WorkspaceOpenData>> OpenAsync(WorkspaceOpenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = NormalizeOpenPath(request.Path);
        if (normalizedPath is null)
        {
            return ToolResult<WorkspaceOpenData>.Rejected(CreateError("WorkspacePathInvalid", "Workspace paths must be absolute .sln, .slnx, or .csproj files."));
        }

        var alias = NormalizeAlias(request.Alias);
        var preflightHostSnapshot = ReadSnapshot();
        var capacityError = ValidateOpenCapacity(preflightHostSnapshot);
        if (capacityError is not null)
        {
            return CreateToolResult<WorkspaceOpenData>(capacityError);
        }

        var uniquenessError = ValidateOpenUniqueness(preflightHostSnapshot, normalizedPath, alias);
        if (uniquenessError is not null)
        {
            return CreateToolResult<WorkspaceOpenData>(uniquenessError);
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

        var session = CreateSessionSnapshot(
            AllocateWorkspaceId(),
            alias,
            loadedWorkspace.Workspace,
            loadedWorkspace.Solution,
            normalizedPath,
            Interlocked.Increment(ref _nextWorkspaceEpoch),
            loadedWorkspace.Diagnostics,
            operationGate: null);

        foreach (var project in session.LoadedWorkspace!.CurrentSolution.Projects
                     .Where(static project => string.Equals(project.Language, LanguageNames.CSharp, StringComparison.Ordinal))
                     .Where(static project => !string.IsNullOrWhiteSpace(project.FilePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projectCompatibility = InspectProjectCompatibility(project.FilePath!);
            if (projectCompatibility.Diagnostics.Count > 0)
            {
                session.LoadedWorkspace.Dispose();
                return ToolResult<WorkspaceOpenData>.Rejected(
                    CreateError(_workspaceLoadFailedCode, "The workspace could not be loaded."),
                    diagnostics: projectCompatibility.Diagnostics);
            }

            if (!projectCompatibility.IsSdkStyle)
            {
                session.LoadedWorkspace.Dispose();
                return ToolResult<WorkspaceOpenData>.Rejected(CreateError(_workspaceNotSupportedCode, "Only SDK-style C# projects are supported."));
            }
        }

        lock (_syncRoot)
        {
            var latestSnapshot = _snapshot;
            var latestCapacityError = ValidateOpenCapacity(latestSnapshot);
            var latestUniquenessError = ValidateOpenUniqueness(latestSnapshot, normalizedPath, alias);
            if (latestCapacityError is not null || latestUniquenessError is not null)
            {
                session.LoadedWorkspace.Dispose();
                return CreateToolResult<WorkspaceOpenData>(latestCapacityError ?? latestUniquenessError!);
            }

            var workspaces = new Dictionary<string, WorkspaceSessionSnapshot>(latestSnapshot.Workspaces, StringComparer.Ordinal)
            {
                [session.Workspace.WorkspaceId] = session,
            };
            _snapshot = latestSnapshot with
            {
                Workspaces = workspaces,
            };
        }

        return ToolResult<WorkspaceOpenData>.Succeeded(
            new WorkspaceOpenData
            {
                Workspace = session.Workspace,
                ProjectCount = session.ProjectCount,
                DocumentCount = session.DocumentCount,
                LoadDiagnostics = session.LoadDiagnostics,
            },
            workspaceId: session.Workspace.WorkspaceId,
            workspaceEpoch: session.Workspace.WorkspaceEpoch);
    }

    public ValueTask<ToolResult<WorkspaceListData>> ListAsync(WorkspaceListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = ReadSnapshot();
        return ValueTask.FromResult(ToolResult<WorkspaceListData>.Succeeded(
            new WorkspaceListData
            {
                Workspaces = snapshot.Workspaces.Values
                    .OrderBy(static session => session.Workspace.WorkspaceId, StringComparer.Ordinal)
                    .Select(static session => session.Workspace)
                    .ToArray(),
                TransactionOwnerWorkspaceId = snapshot.TransactionOwnerWorkspaceId,
            }));
    }

    public async ValueTask<ToolResult<WorkspaceCloseData>> CloseAsync(WorkspaceCloseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var selectionError = TryResolveWorkspaceSelection(ReadSnapshot(), request.Workspace, out var selection);
        if (selectionError is not null)
        {
            return CreateToolResult<WorkspaceCloseData>(selectionError);
        }

        var lease = selection!.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return CreateToolResult<WorkspaceCloseData>(CreateBusyResult(selection.Session), selection.Session);
        }

        await using var leaseScope = lease;
        var session = ReadSession(selection.WorkspaceId);
        if (session is null)
        {
            return CreateToolResult<WorkspaceCloseData>(CreateWorkspaceRequiredResult());
        }

        if (session.State is WorkspaceLifecycleState.TransactionActive or WorkspaceLifecycleState.TransactionConflicted)
        {
            return CreateToolResult<WorkspaceCloseData>(CreateCommitOrRollbackRequiredResult(session), session);
        }

        lock (_syncRoot)
        {
            if (!_snapshot.Workspaces.TryGetValue(selection.WorkspaceId, out var currentSession))
            {
                return CreateToolResult<WorkspaceCloseData>(CreateWorkspaceRequiredResult());
            }

            var workspaces = new Dictionary<string, WorkspaceSessionSnapshot>(_snapshot.Workspaces, StringComparer.Ordinal);
            workspaces.Remove(selection.WorkspaceId);
            _snapshot = _snapshot with
            {
                Workspaces = workspaces,
                TransactionOwnerWorkspaceId = string.Equals(_snapshot.TransactionOwnerWorkspaceId, selection.WorkspaceId, StringComparison.Ordinal)
                    ? null
                    : _snapshot.TransactionOwnerWorkspaceId,
            };

            currentSession.LoadedWorkspace?.Dispose();
            session = currentSession;
        }

        return ToolResult<WorkspaceCloseData>.Succeeded(
            new WorkspaceCloseData
            {
                ClosedPath = session.Workspace.LoadedPath,
            },
            workspaceId: session.Workspace.WorkspaceId,
            workspaceEpoch: session.Workspace.WorkspaceEpoch);
    }

    public async ValueTask<ToolResult<WorkspaceStatusData>> GetStatusAsync(WorkspaceStatusRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var selectionError = TryResolveWorkspaceSelection(ReadSnapshot(), request.Workspace, out var selection);
        if (selectionError is not null)
        {
            return CreateToolResult<WorkspaceStatusData>(selectionError);
        }

        var lease = selection!.Session.OperationGate.TryAcquireShared();
        if (lease is null)
        {
            return CreateToolResult<WorkspaceStatusData>(CreateBusyResult(selection.Session), selection.Session);
        }

        await using var leaseScope = lease;
        var session = ReadSession(selection.WorkspaceId);
        if (session is null)
        {
            return CreateToolResult<WorkspaceStatusData>(CreateWorkspaceRequiredResult());
        }

        if (session.State is WorkspaceLifecycleState.Ready or WorkspaceLifecycleState.TransactionActive && HasExternalChange(session, cancellationToken))
        {
            MarkExternalChangeDetected(selection.WorkspaceId);
            session = ReadSession(selection.WorkspaceId);
        }

        if (session is null)
        {
            return CreateToolResult<WorkspaceStatusData>(CreateWorkspaceRequiredResult());
        }

        return ToolResult<WorkspaceStatusData>.Succeeded(
            CreateStatusData(session),
            workspaceId: session.Workspace.WorkspaceId,
            workspaceEpoch: session.Workspace.WorkspaceEpoch,
            transactionRevision: session.Transaction?.CurrentRevision);
    }

    public async ValueTask<ToolResult<WorkspaceReloadData>> ReloadAsync(WorkspaceReloadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var selectionError = TryResolveWorkspaceSelection(ReadSnapshot(), request.Workspace, out var selection);
        if (selectionError is not null)
        {
            return CreateToolResult<WorkspaceReloadData>(selectionError);
        }

        var lease = selection!.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return CreateToolResult<WorkspaceReloadData>(CreateBusyResult(selection.Session), selection.Session);
        }

        await using var leaseScope = lease;
        var currentSession = ReadSession(selection.WorkspaceId);
        if (currentSession is null)
        {
            return CreateToolResult<WorkspaceReloadData>(CreateWorkspaceRequiredResult());
        }

        if (currentSession.State is WorkspaceLifecycleState.TransactionActive or WorkspaceLifecycleState.TransactionConflicted)
        {
            return ToolResult<WorkspaceReloadData>.Rejected(
                CreateError("WorkspaceReloadBlocked", "Commit or roll back the active transaction before reloading."),
                RequiredAction.CommitOrRollback,
                workspaceId: currentSession.Workspace.WorkspaceId,
                workspaceEpoch: currentSession.Workspace.WorkspaceEpoch,
                transactionRevision: currentSession.Transaction?.CurrentRevision);
        }

        if (currentSession.State != WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return ToolResult<WorkspaceReloadData>.Rejected(
                CreateError("WorkspaceReloadNotRequired", "The workspace does not require reload."),
                workspaceId: currentSession.Workspace.WorkspaceId,
                workspaceEpoch: currentSession.Workspace.WorkspaceEpoch);
        }

        if (string.Equals(Path.GetExtension(currentSession.Workspace.LoadedPath), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var preflight = InspectProjectCompatibility(currentSession.Workspace.LoadedPath);
            if (preflight.Diagnostics.Count > 0)
            {
                return ToolResult<WorkspaceReloadData>.Rejected(
                    CreateError(_workspaceLoadFailedCode, "The workspace could not be reloaded."),
                    workspaceId: currentSession.Workspace.WorkspaceId,
                    workspaceEpoch: currentSession.Workspace.WorkspaceEpoch,
                    diagnostics: preflight.Diagnostics);
            }

            if (!preflight.IsSdkStyle)
            {
                return ToolResult<WorkspaceReloadData>.Rejected(
                    CreateError(_workspaceNotSupportedCode, "Only SDK-style C# projects are supported."),
                    workspaceId: currentSession.Workspace.WorkspaceId,
                    workspaceEpoch: currentSession.Workspace.WorkspaceEpoch);
            }
        }

        var loadedWorkspace = await LoadWorkspaceAsync(currentSession.Workspace.LoadedPath, cancellationToken);
        if (loadedWorkspace.Solution is null || loadedWorkspace.Workspace is null)
        {
            return ToolResult<WorkspaceReloadData>.Rejected(
                CreateError(_workspaceLoadFailedCode, "The workspace could not be reloaded."),
                workspaceId: currentSession.Workspace.WorkspaceId,
                workspaceEpoch: currentSession.Workspace.WorkspaceEpoch,
                diagnostics: loadedWorkspace.Diagnostics);
        }

        var reloadedSession = CreateSessionSnapshot(
            currentSession.Workspace.WorkspaceId,
            currentSession.Workspace.Alias,
            loadedWorkspace.Workspace,
            loadedWorkspace.Solution,
            currentSession.Workspace.LoadedPath,
            Interlocked.Increment(ref _nextWorkspaceEpoch),
            loadedWorkspace.Diagnostics,
            currentSession.OperationGate);

        lock (_syncRoot)
        {
            if (_snapshot.Workspaces.TryGetValue(selection.WorkspaceId, out var latestSession))
            {
                latestSession.LoadedWorkspace?.Dispose();
            }

            ReplaceSessionLocked(selection.WorkspaceId, reloadedSession);
        }

        return ToolResult<WorkspaceReloadData>.Succeeded(
            new WorkspaceReloadData
            {
                Workspace = reloadedSession.Workspace,
                ProjectCount = reloadedSession.ProjectCount,
                DocumentCount = reloadedSession.DocumentCount,
                LoadDiagnostics = reloadedSession.LoadDiagnostics,
            },
            workspaceId: reloadedSession.Workspace.WorkspaceId,
            workspaceEpoch: reloadedSession.Workspace.WorkspaceEpoch);
    }

    public async ValueTask<ToolResult<TransactionStartData>> StartTransactionAsync(TransactionStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var selectionError = TryResolveWorkspaceSelection(ReadSnapshot(), request.Workspace, out var selection);
        if (selectionError is not null)
        {
            return CreateToolResult<TransactionStartData>(selectionError);
        }

        var lease = selection!.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return CreateToolResult<TransactionStartData>(CreateBusyResult(selection.Session), selection.Session);
        }

        await using var leaseScope = lease;
        var session = ReadSession(selection.WorkspaceId);
        if (session is null || session.CurrentSolution is null)
        {
            return CreateToolResult<TransactionStartData>(CreateWorkspaceRequiredResult());
        }

        if (session.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return ToolResult<TransactionStartData>.Conflict(
                CreateError(_workspaceOutOfDateCode, "Reload the workspace before starting a transaction."),
                RequiredAction.ReloadWorkspace,
                workspaceId: session.Workspace.WorkspaceId,
                workspaceEpoch: session.Workspace.WorkspaceEpoch);
        }

        var ownerWorkspaceId = ReadSnapshot().TransactionOwnerWorkspaceId;
        if (!string.IsNullOrWhiteSpace(ownerWorkspaceId) && !string.Equals(ownerWorkspaceId, selection.WorkspaceId, StringComparison.Ordinal))
        {
            var ownerSession = ReadSession(ownerWorkspaceId);
            return ToolResult<TransactionStartData>.Rejected(
                CreateError(_transactionOwnerCode, $"Commit or roll back the transaction on workspace '{GetWorkspaceDisplayName(ownerSession)}' before starting a transaction on this workspace."),
                RequiredAction.CommitOrRollback,
                workspaceId: session.Workspace.WorkspaceId,
                workspaceEpoch: session.Workspace.WorkspaceEpoch,
                transactionRevision: session.Transaction?.CurrentRevision);
        }

        if (session.Transaction is not null)
        {
            return ToolResult<TransactionStartData>.Rejected(
                CreateError(_transactionAlreadyActiveCode, "A transaction is already active."),
                RequiredAction.CommitOrRollback,
                workspaceId: session.Workspace.WorkspaceId,
                workspaceEpoch: session.Workspace.WorkspaceEpoch,
                transactionRevision: session.Transaction.CurrentRevision);
        }

        var transaction = new WorkspaceTransaction
        {
            BaselineSolution = session.CurrentSolution,
            CurrentRevision = 0,
            MaxRevisions = _options.MaxTransactionRevisions,
        };
        var updatedSession = session with
        {
            Transaction = transaction,
            CurrentSolution = transaction.CurrentSolution,
            State = WorkspaceStateMachine.Fire(session.State, WorkspaceTrigger.TransactionStarted),
        };

        lock (_syncRoot)
        {
            ReplaceSessionLocked(selection.WorkspaceId, updatedSession);
            _snapshot = _snapshot with
            {
                TransactionOwnerWorkspaceId = selection.WorkspaceId,
            };
        }

        return ToolResult<TransactionStartData>.Succeeded(
            new TransactionStartData
            {
                Transaction = transaction.ToInfo(conflicted: false),
            },
            workspaceId: updatedSession.Workspace.WorkspaceId,
            workspaceEpoch: updatedSession.Workspace.WorkspaceEpoch,
            transactionRevision: transaction.CurrentRevision);
    }

    public async ValueTask<ToolResult<TransactionPreviewData>> PreviewTransactionAsync(TransactionPreviewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var selectionError = TryResolveWorkspaceSelection(ReadSnapshot(), request.Workspace, out var selection);
        if (selectionError is not null)
        {
            return CreateToolResult<TransactionPreviewData>(selectionError);
        }

        var lease = selection!.Session.OperationGate.TryAcquireShared();
        if (lease is null)
        {
            return CreateToolResult<TransactionPreviewData>(CreateBusyResult(selection.Session), selection.Session);
        }

        await using var leaseScope = lease;
        var session = ReadSession(selection.WorkspaceId);
        if (session?.Transaction is null)
        {
            return ToolResult<TransactionPreviewData>.Rejected(
                CreateError(_transactionRequiredCode, "Start a transaction before previewing changes."),
                RequiredAction.StartTransaction,
                workspaceId: session?.Workspace.WorkspaceId,
                workspaceEpoch: session?.Workspace.WorkspaceEpoch);
        }

        var resolver = new WorkspaceResolver(session.Transaction.CurrentSolution, session.Workspace, session.Transaction.CurrentRevision);
        var changes = await WorkspaceDiffBuilder.CreateChangeSummaryAsync(
            session.Transaction.BaselineSolution,
            session.Transaction.CurrentSolution,
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
                        session.Transaction.BaselineSolution,
                        session.Transaction.CurrentSolution,
                        reference,
                        resolver,
                        request.ContextLines,
                        cancellationToken);
            }
        }

        return ToolResult<TransactionPreviewData>.Succeeded(
            new TransactionPreviewData
            {
                Transaction = session.Transaction.ToInfo(session.State == WorkspaceLifecycleState.TransactionConflicted),
                Documents = documents,
                Diff = diff,
            },
            workspaceId: session.Workspace.WorkspaceId,
            workspaceEpoch: session.Workspace.WorkspaceEpoch,
            transactionRevision: session.Transaction.CurrentRevision);
    }

    public async ValueTask<ToolResult<TransactionHistoryData>> MoveTransactionHistoryAsync(TransactionHistoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var selectionError = TryResolveWorkspaceSelection(ReadSnapshot(), request.Workspace, out var selection);
        if (selectionError is not null)
        {
            return CreateToolResult<TransactionHistoryData>(selectionError);
        }

        var lease = selection!.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return CreateToolResult<TransactionHistoryData>(CreateBusyResult(selection.Session), selection.Session);
        }

        await using var leaseScope = lease;
        var session = ReadSession(selection.WorkspaceId);
        var transaction = session?.Transaction;
        if (session is null || transaction is null)
        {
            return ToolResult<TransactionHistoryData>.Rejected(CreateError(_transactionRequiredCode, "Start a transaction before moving history."), RequiredAction.StartTransaction);
        }

        var snapshotMismatch = ValidateSnapshotPrecondition(session, request.ExpectedSnapshot);
        if (snapshotMismatch is not null)
        {
            return CreateToolResult<TransactionHistoryData>(snapshotMismatch, session);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return ToolResult<TransactionHistoryData>.Conflict(
                CreateError(_transactionConflictedCode, "Roll back the conflicted transaction before changing history."),
                RequiredAction.RollbackTransaction,
                workspaceId: session.Workspace.WorkspaceId,
                workspaceEpoch: session.Workspace.WorkspaceEpoch,
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
                workspaceId: session.Workspace.WorkspaceId,
                workspaceEpoch: session.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision);
        }

        var updatedTransaction = transaction with
        {
            CurrentRevision = nextRevision,
        };
        var updatedSession = session with
        {
            Transaction = updatedTransaction,
            CurrentSolution = updatedTransaction.CurrentSolution,
        };

        lock (_syncRoot)
        {
            ReplaceSessionLocked(selection.WorkspaceId, updatedSession);
        }

        return ToolResult<TransactionHistoryData>.Succeeded(
            new TransactionHistoryData
            {
                Transaction = updatedTransaction.ToInfo(conflicted: false),
            },
            workspaceId: updatedSession.Workspace.WorkspaceId,
            workspaceEpoch: updatedSession.Workspace.WorkspaceEpoch,
            transactionRevision: updatedTransaction.CurrentRevision);
    }

    public async ValueTask<ToolResult<TransactionCommitData>> CommitTransactionAsync(TransactionCommitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var selectionError = TryResolveWorkspaceSelection(ReadSnapshot(), request.Workspace, out var selection);
        if (selectionError is not null)
        {
            return CreateToolResult<TransactionCommitData>(selectionError);
        }

        var lease = selection!.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return CreateToolResult<TransactionCommitData>(CreateBusyResult(selection.Session), selection.Session);
        }

        await using var leaseScope = lease;
        var session = ReadSession(selection.WorkspaceId);
        var transaction = session?.Transaction;
        if (session is null || transaction is null)
        {
            return ToolResult<TransactionCommitData>.Rejected(CreateError(_transactionRequiredCode, "Start a transaction before committing changes."), RequiredAction.StartTransaction);
        }

        var snapshotMismatch = ValidateSnapshotPrecondition(session, request.ExpectedSnapshot);
        if (snapshotMismatch is not null)
        {
            return CreateToolResult<TransactionCommitData>(snapshotMismatch, session);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return ToolResult<TransactionCommitData>.Conflict(
                CreateError(_transactionConflictedCode, "Roll back the conflicted transaction before committing changes."),
                RequiredAction.RollbackTransaction,
                workspaceId: session.Workspace.WorkspaceId,
                workspaceEpoch: session.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision);
        }

        if (transaction.CurrentRevision == 0)
        {
            return ToolResult<TransactionCommitData>.NoChange(
                workspaceId: session.Workspace.WorkspaceId,
                workspaceEpoch: session.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision,
                data: new TransactionCommitData
                {
                    Committed = false,
                    Transaction = transaction.ToInfo(conflicted: false),
                });
        }

        if (HasExternalChange(session, cancellationToken))
        {
            MarkExternalChangeDetected(selection.WorkspaceId);
            session = ReadSession(selection.WorkspaceId);
            transaction = session!.Transaction!;

            return ToolResult<TransactionCommitData>.Conflict(
                CreateError(_transactionConflictedCode, "The transaction conflicted with external workspace changes."),
                RequiredAction.RollbackTransaction,
                workspaceId: session.Workspace.WorkspaceId,
                workspaceEpoch: session.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision);
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
                InputManifest = WorkspaceInputManifestBuilder.Build(transaction.CurrentSolution, session.Workspace.LoadedPath),
                State = WorkspaceStateMachine.Fire(session.State, WorkspaceTrigger.TransactionCommitted),
            };

            lock (_syncRoot)
            {
                ReplaceSessionLocked(selection.WorkspaceId, committedSession);
                if (string.Equals(_snapshot.TransactionOwnerWorkspaceId, selection.WorkspaceId, StringComparison.Ordinal))
                {
                    _snapshot = _snapshot with
                    {
                        TransactionOwnerWorkspaceId = null,
                    };
                }
            }

            CommitRecoveryStore.DeleteStatus(_options.StateDirectory, commitId);

            return ToolResult<TransactionCommitData>.Succeeded(
                new TransactionCommitData
                {
                    Committed = true,
                },
                workspaceId: committedSession.Workspace.WorkspaceId,
                workspaceEpoch: committedSession.Workspace.WorkspaceEpoch);
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

            return ToolResult<TransactionCommitData>.Faulted(
                CreateError("CommitFailed", "The transaction commit could not be completed."),
                RequiredAction.ResolveRecovery,
                workspaceId: session.Workspace.WorkspaceId,
                workspaceEpoch: session.Workspace.WorkspaceEpoch,
                transactionRevision: transaction.CurrentRevision);
        }
    }

    public async ValueTask<ToolResult<TransactionRollbackData>> RollbackTransactionAsync(TransactionRollbackRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var selectionError = TryResolveWorkspaceSelection(ReadSnapshot(), request.Workspace, out var selection);
        if (selectionError is not null)
        {
            return CreateToolResult<TransactionRollbackData>(selectionError);
        }

        var lease = selection!.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return CreateToolResult<TransactionRollbackData>(CreateBusyResult(selection.Session), selection.Session);
        }

        await using var leaseScope = lease;
        var session = ReadSession(selection.WorkspaceId);
        var transaction = session?.Transaction;
        if (session is null || transaction is null)
        {
            return ToolResult<TransactionRollbackData>.Rejected(CreateError(_transactionRequiredCode, "Start a transaction before rolling back changes."), RequiredAction.StartTransaction);
        }

        var rollbackState = session.State == WorkspaceLifecycleState.TransactionConflicted
            ? TransactionRollbackState.WorkspaceOutOfDate
            : TransactionRollbackState.Ready;
        var updatedSession = session with
        {
            Transaction = null,
            CurrentSolution = transaction.BaselineSolution,
            State = WorkspaceStateMachine.Fire(
                session.State,
                session.State == WorkspaceLifecycleState.TransactionConflicted
                    ? WorkspaceTrigger.ConflictedRollbackCompleted
                    : WorkspaceTrigger.TransactionRolledBack),
        };

        lock (_syncRoot)
        {
            ReplaceSessionLocked(selection.WorkspaceId, updatedSession);
            if (string.Equals(_snapshot.TransactionOwnerWorkspaceId, selection.WorkspaceId, StringComparison.Ordinal))
            {
                _snapshot = _snapshot with
                {
                    TransactionOwnerWorkspaceId = null,
                };
            }
        }

        return ToolResult<TransactionRollbackData>.Succeeded(
            new TransactionRollbackData
            {
                State = rollbackState,
            },
            workspaceId: updatedSession.Workspace.WorkspaceId,
            workspaceEpoch: updatedSession.Workspace.WorkspaceEpoch);
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

    private static string? NormalizeAlias(string? alias)
    {
        return string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
    }

    private static string NormalizeSelectorPath(string path)
    {
        return Path.IsPathRooted(path) ? Path.GetFullPath(path) : path;
    }

    private static WorkspaceSelector? GetWorkspaceSelector(object request)
    {
        return request.GetType().GetProperty("Workspace")?.GetValue(request) as WorkspaceSelector;
    }

    private WorkspaceQueryContext CreateQueryContext(WorkspaceSessionSnapshot session)
    {
        var resolver = new WorkspaceResolver(session.CurrentSolution!, session.Workspace, session.Transaction?.CurrentRevision);
        return new WorkspaceQueryContext(
            session.CurrentSolution!,
            session.Workspace,
            session.Transaction?.CurrentRevision,
            new ResultLimit
            {
                MaxResults = _options.DefaultMaxResults,
            },
            _options.MaxResponseBytes,
            resolver,
            _codeActionService);
    }

    private WorkspaceMutationContext CreateMutationContext(WorkspaceSessionSnapshot session)
    {
        var resolver = new WorkspaceResolver(session.CurrentSolution!, session.Workspace, session.Transaction?.CurrentRevision);
        return new WorkspaceMutationContext(
            session.CurrentSolution!,
            session.Workspace,
            session.Transaction?.CurrentRevision,
            new ResultLimit
            {
                MaxResults = _options.DefaultMaxResults,
            },
            resolver,
            _codeActionService,
            StageMutationAsync);
    }

    private static WorkspaceStatusData CreateStatusData(WorkspaceSessionSnapshot session)
    {
        return new WorkspaceStatusData
        {
            State = session.State,
            Workspace = session.Workspace,
            ProjectCount = session.ProjectCount,
            DocumentCount = session.DocumentCount,
            LoadDiagnostics = session.LoadDiagnostics,
            Transaction = session.Transaction?.ToInfo(session.State == WorkspaceLifecycleState.TransactionConflicted),
            ReloadRequired = session.State == WorkspaceLifecycleState.WorkspaceOutOfDate,
        };
    }

    private WorkspaceSessionSnapshot CreateSessionSnapshot(
        string workspaceId,
        string? alias,
        MSBuildWorkspace workspace,
        Solution solution,
        string loadedPath,
        long workspaceEpoch,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        WorkspaceOperationGate? operationGate)
    {
        return new WorkspaceSessionSnapshot
        {
            State = WorkspaceLifecycleState.Ready,
            Workspace = new WorkspaceIdentity
            {
                WorkspaceId = workspaceId,
                Alias = alias,
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
            OperationGate = operationGate ?? new WorkspaceOperationGate(_options.MaxConcurrentQueries),
        };
    }

    private PluginExecutionResultBox CreateBusyResult(WorkspaceSessionSnapshot? session = null)
    {
        _ = session;
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

    private static PluginExecutionResultBox CreateWorkspaceOutOfDateResult(WorkspaceSessionSnapshot session)
    {
        return new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Conflict,
            Error = CreateError(_workspaceOutOfDateCode, "Reload the workspace before invoking this tool."),
            RequiredAction = RequiredAction.ReloadWorkspace,
        };
    }

    private static PluginExecutionResultBox CreateTransactionConflictedResult(WorkspaceSessionSnapshot session)
    {
        return new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Conflict,
            Error = CreateError(_transactionConflictedCode, "Roll back the conflicted transaction before invoking this tool."),
            RequiredAction = RequiredAction.RollbackTransaction,
        };
    }

    private static PluginExecutionResultBox CreateCommitOrRollbackRequiredResult(WorkspaceSessionSnapshot session)
    {
        return new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Rejected,
            Error = CreateError("TransactionOpen", "Commit or roll back the active transaction before invoking this tool."),
            RequiredAction = RequiredAction.CommitOrRollback,
        };
    }

    private PluginExecutionResultBox? TryResolveWorkspaceSelection(
        WorkspaceHostSnapshot hostSnapshot,
        WorkspaceSelector? selector,
        out WorkspaceSelection? selection)
    {
        selection = null;
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return CreateWorkspaceRequiredResult();
        }

        if (selector is null)
        {
            if (hostSnapshot.Workspaces.Count == 1)
            {
                var pair = hostSnapshot.Workspaces.Single();
                selection = new WorkspaceSelection
                {
                    WorkspaceId = pair.Key,
                    Session = pair.Value,
                };
                return null;
            }

            return new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = CreateError(_workspaceSelectorRequiredCode, "Select a workspace when more than one workspace is loaded."),
                RequiredAction = RequiredAction.ResolveTargetAgain,
            };
        }

        var resolvedWorkspaceId = ResolveWorkspaceId(hostSnapshot, selector);
        if (resolvedWorkspaceId.error is not null)
        {
            return resolvedWorkspaceId.error;
        }

        var workspaceId = resolvedWorkspaceId.workspaceId!;
        selection = new WorkspaceSelection
        {
            WorkspaceId = workspaceId,
            Session = hostSnapshot.Workspaces[workspaceId],
        };
        return null;
    }

    private (string? workspaceId, PluginExecutionResultBox? error) ResolveWorkspaceId(WorkspaceHostSnapshot hostSnapshot, WorkspaceSelector selector)
    {
        string? resolvedWorkspaceId = null;

        static bool IsProvided(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        void MatchWorkspaceId(string? candidateWorkspaceId)
        {
            if (candidateWorkspaceId is null)
            {
                return;
            }

            if (resolvedWorkspaceId is null)
            {
                resolvedWorkspaceId = candidateWorkspaceId;
                return;
            }

            if (!string.Equals(resolvedWorkspaceId, candidateWorkspaceId, StringComparison.Ordinal))
            {
                resolvedWorkspaceId = string.Empty;
            }
        }

        if (IsProvided(selector.WorkspaceId))
        {
            if (!hostSnapshot.Workspaces.ContainsKey(selector.WorkspaceId!))
            {
                return (null, new PluginExecutionResultBox
                {
                    Outcome = ToolOutcome.Rejected,
                    Error = CreateError(_workspaceSelectorNotFoundCode, "The workspace selector did not match any loaded workspace."),
                    RequiredAction = RequiredAction.ResolveTargetAgain,
                });
            }

            MatchWorkspaceId(selector.WorkspaceId);
        }

        if (IsProvided(selector.Alias))
        {
            var aliasMatch = hostSnapshot.Workspaces.SingleOrDefault(pair => string.Equals(pair.Value.Workspace.Alias, selector.Alias, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(aliasMatch.Key))
            {
                return (null, new PluginExecutionResultBox
                {
                    Outcome = ToolOutcome.Rejected,
                    Error = CreateError(_workspaceSelectorNotFoundCode, "The workspace selector did not match any loaded workspace."),
                    RequiredAction = RequiredAction.ResolveTargetAgain,
                });
            }

            MatchWorkspaceId(aliasMatch.Key);
        }

        if (IsProvided(selector.Path))
        {
            var normalizedPath = NormalizeSelectorPath(selector.Path!);
            var pathMatch = hostSnapshot.Workspaces.SingleOrDefault(pair => string.Equals(pair.Value.Workspace.LoadedPath, normalizedPath, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(pathMatch.Key))
            {
                return (null, new PluginExecutionResultBox
                {
                    Outcome = ToolOutcome.Rejected,
                    Error = CreateError(_workspaceSelectorNotFoundCode, "The workspace selector did not match any loaded workspace."),
                    RequiredAction = RequiredAction.ResolveTargetAgain,
                });
            }

            MatchWorkspaceId(pathMatch.Key);
        }

        if (resolvedWorkspaceId is null)
        {
            return (null, new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = CreateError(_workspaceSelectorNotFoundCode, "The workspace selector did not match any loaded workspace."),
                RequiredAction = RequiredAction.ResolveTargetAgain,
            });
        }

        if (resolvedWorkspaceId.Length == 0)
        {
            return (null, new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = CreateError(_workspaceSelectorMismatchCode, "The workspace selector fields must resolve to the same loaded workspace."),
                RequiredAction = RequiredAction.ResolveTargetAgain,
            });
        }

        return (resolvedWorkspaceId, null);
    }

    private PluginExecutionResultBox? ValidateOpenCapacity(WorkspaceHostSnapshot hostSnapshot)
    {
        return hostSnapshot.Workspaces.Count >= _options.MaxLoadedWorkspaces
            ? new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = CreateError(_workspaceCapacityCode, "Close an existing workspace before opening another one."),
            }
            : null;
    }

    private PluginExecutionResultBox? ValidateOpenUniqueness(WorkspaceHostSnapshot hostSnapshot, string normalizedPath, string? alias)
    {
        if (hostSnapshot.Workspaces.Values.Any(session => string.Equals(session.Workspace.LoadedPath, normalizedPath, StringComparison.Ordinal)))
        {
            return new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = CreateError(_workspaceAlreadyOpenCode, "A workspace for this path is already open."),
            };
        }

        if (alias is not null
            && hostSnapshot.Workspaces.Values.Any(session => string.Equals(session.Workspace.Alias, alias, StringComparison.Ordinal)))
        {
            return new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = CreateError(_workspaceAlreadyOpenCode, "A workspace with this alias is already open."),
            };
        }

        return null;
    }

    private static string GetWorkspaceDisplayName(WorkspaceSessionSnapshot? session)
    {
        if (session is null)
        {
            return "unknown";
        }

        return session.Workspace.Alias
            ?? session.Workspace.LoadedPath
            ?? session.Workspace.WorkspaceId;
    }

    private bool HasExternalChange(WorkspaceSessionSnapshot session, CancellationToken cancellationToken)
    {
        return WorkspaceInputManifestValidator.HasChanged(session.InputManifest, cancellationToken);
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

    private void MarkExternalChangeDetected(string workspaceId)
    {
        lock (_syncRoot)
        {
            if (!_snapshot.Workspaces.TryGetValue(workspaceId, out var session))
            {
                return;
            }

            var trigger = session.State switch
            {
                WorkspaceLifecycleState.Ready => WorkspaceTrigger.ExternalChangeDetected,
                WorkspaceLifecycleState.TransactionActive => WorkspaceTrigger.TransactionConflictDetected,
                _ => (WorkspaceTrigger?)null,
            };
            if (trigger is null)
            {
                return;
            }

            ReplaceSessionLocked(
                workspaceId,
                session with
                {
                    State = WorkspaceStateMachine.Fire(session.State, trigger.Value),
                });
        }
    }

    private WorkspaceHostSnapshot ReadSnapshot()
    {
        lock (_syncRoot)
        {
            return _snapshot;
        }
    }

    private WorkspaceSessionSnapshot? ReadSession(string workspaceId)
    {
        lock (_syncRoot)
        {
            return _snapshot.Workspaces.TryGetValue(workspaceId, out var session)
                ? session
                : null;
        }
    }

    private void ReplaceSessionLocked(string workspaceId, WorkspaceSessionSnapshot session)
    {
        var workspaces = new Dictionary<string, WorkspaceSessionSnapshot>(_snapshot.Workspaces, StringComparer.Ordinal)
        {
            [workspaceId] = session,
        };
        _snapshot = _snapshot with
        {
            Workspaces = workspaces,
        };
    }

    private PluginExecutionResultBox? ValidateMutationSession(string workspaceId, WorkspaceSessionSnapshot session, CancellationToken cancellationToken)
    {
        var ownerWorkspaceId = ReadSnapshot().TransactionOwnerWorkspaceId;
        if (!string.IsNullOrWhiteSpace(ownerWorkspaceId) && !string.Equals(ownerWorkspaceId, workspaceId, StringComparison.Ordinal))
        {
            var ownerSession = ReadSession(ownerWorkspaceId);
            return new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = CreateError(_transactionOwnerCode, $"Commit or roll back the transaction on workspace '{GetWorkspaceDisplayName(ownerSession)}' before mutating this workspace."),
                RequiredAction = RequiredAction.CommitOrRollback,
            };
        }

        if (session.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return CreateWorkspaceOutOfDateResult(session);
        }

        if (HasExternalChange(session, cancellationToken))
        {
            MarkExternalChangeDetected(workspaceId);
            session = ReadSession(workspaceId)!;
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return CreateTransactionConflictedResult(session);
        }

        if (session.Transaction is null)
        {
            return CreateNoActiveTransactionResult();
        }

        if (session.Transaction.CurrentRevision >= session.Transaction.MaxRevisions)
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

    private static PluginExecutionResultBox? ValidateSnapshotPrecondition(WorkspaceSessionSnapshot session, SnapshotPrecondition? expectedSnapshot)
    {
        if (session.Transaction is null || expectedSnapshot is null)
        {
            return null;
        }

        if ((!string.IsNullOrWhiteSpace(expectedSnapshot.WorkspaceId) && !string.Equals(expectedSnapshot.WorkspaceId, session.Workspace.WorkspaceId, StringComparison.Ordinal))
            || expectedSnapshot.WorkspaceEpoch != session.Workspace.WorkspaceEpoch
            || expectedSnapshot.TransactionRevision != session.Transaction.CurrentRevision)
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
        var hostSnapshot = ReadSnapshot();
        if (string.IsNullOrWhiteSpace(hostSnapshot.TransactionOwnerWorkspaceId))
        {
            return PluginExecutionResult<MutationData>.Rejected(CreateError(_transactionRequiredCode, "Start a transaction before invoking mutation tools."), RequiredAction.StartTransaction);
        }

        var session = ReadSession(hostSnapshot.TransactionOwnerWorkspaceId!);
        if (session?.Transaction is null || session.CurrentSolution is null)
        {
            return PluginExecutionResult<MutationData>.Rejected(CreateError(_transactionRequiredCode, "Start a transaction before invoking mutation tools."), RequiredAction.StartTransaction);
        }

        var validationError = ValidateMutationProposal(session.CurrentSolution, proposal);
        if (validationError is not null)
        {
            return PluginExecutionResult<MutationData>.Rejected(validationError.Value.error, validationError.Value.requiredAction, diagnostics, warnings);
        }

        var transaction = session.Transaction;
        var stagedChanges = await WorkspaceDiffBuilder.CreateChangeSummaryAsync(
            transaction.BaselineSolution,
            proposal.CandidateSolution!,
            new WorkspaceResolver(proposal.CandidateSolution!, session.Workspace, transaction.CurrentRevision + 1),
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
        var updatedSession = session with
        {
            Transaction = updatedTransaction,
            CurrentSolution = updatedTransaction.CurrentSolution,
        };

        lock (_syncRoot)
        {
            ReplaceSessionLocked(hostSnapshot.TransactionOwnerWorkspaceId!, updatedSession);
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

    private static DiagnosticInfo CreateLoadDiagnostic(string message)
    {
        return new DiagnosticInfo
        {
            Id = "WorkspaceLoad",
            Severity = Contracts.Results.DiagnosticSeverity.Error,
            Message = message,
        };
    }

    private string AllocateWorkspaceId()
    {
        var nextValue = Interlocked.Increment(ref _nextWorkspaceId);
        return $"workspace-{nextValue}";
    }

    private static ToolResult<TData> CreateToolResult<TData>(PluginExecutionResultBox result, WorkspaceSessionSnapshot? session = null)
    {
        return result.Outcome switch
        {
            ToolOutcome.Rejected => ToolResult<TData>.Rejected(
                result.Error!,
                result.RequiredAction,
                workspaceId: session?.Workspace.WorkspaceId,
                workspaceEpoch: session?.Workspace.WorkspaceEpoch,
                transactionRevision: session?.Transaction?.CurrentRevision,
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            ToolOutcome.Conflict => ToolResult<TData>.Conflict(
                result.Error!,
                result.RequiredAction,
                workspaceId: session?.Workspace.WorkspaceId,
                workspaceEpoch: session?.Workspace.WorkspaceEpoch,
                transactionRevision: session?.Transaction?.CurrentRevision,
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            ToolOutcome.Faulted => ToolResult<TData>.Faulted(
                result.Error!,
                result.RequiredAction,
                workspaceId: session?.Workspace.WorkspaceId,
                workspaceEpoch: session?.Workspace.WorkspaceEpoch,
                transactionRevision: session?.Transaction?.CurrentRevision,
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            ToolOutcome.NoChange => ToolResult<TData>.NoChange(
                workspaceId: session?.Workspace.WorkspaceId,
                workspaceEpoch: session?.Workspace.WorkspaceEpoch,
                transactionRevision: session?.Transaction?.CurrentRevision,
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            _ => throw new InvalidOperationException($"Unsupported tool outcome '{result.Outcome}'."),
        };
    }
}
