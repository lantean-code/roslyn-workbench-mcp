using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

internal sealed class WorkspaceLifecycleService : IWorkspaceLifecycleService
{
    private readonly WorkspaceCoordinatorOptions _options;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSessionAcquirer _sessionAcquirer;
    private readonly IWorkspaceLoader _workspaceLoader;
    private readonly IWorkspaceRootResolver _workspaceRootResolver;
    private readonly IWorkspaceLoadWorkflow _workspaceLoadWorkflow;
    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;

    public WorkspaceLifecycleService(
        IOptions<WorkspaceCoordinatorOptions> options,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceSessionAcquirer sessionAcquirer,
        IWorkspaceLoader workspaceLoader,
        IWorkspaceRootResolver workspaceRootResolver,
        IWorkspaceLoadWorkflow workspaceLoadWorkflow,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        IWorkspaceOperationResultFactory resultFactory,
        ICommitRecoveryStore recoveryStore,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher)
    {
        _options = options.Value;
        _sessionStore = sessionStore;
        _sessionAcquirer = sessionAcquirer;
        _workspaceLoader = workspaceLoader;
        _workspaceRootResolver = workspaceRootResolver;
        _workspaceLoadWorkflow = workspaceLoadWorkflow;
        _workspaceChangeDetector = workspaceChangeDetector;
        _workspaceStateTransitions = workspaceStateTransitions;
        _resultFactory = resultFactory;
        _recoveryStore = recoveryStore;
        _instanceStatusPublisher = instanceStatusPublisher;
    }

    public async ValueTask<WorkspaceOperationResult<WorkspaceOpenOutcome>> OpenAsync(
        string path,
        string? alias,
        string? workspaceRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = ResolveOpenRequest(path, alias, workspaceRoot);
        if (request.HasError)
        {
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(request.Error);
        }

        var preflightError = ValidateOpenPreflight(request.LoadedPath, request.Alias);
        if (preflightError is not null)
        {
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(preflightError);
        }

        var hasPendingRecovery = await HasPendingRecoveryAsync(
            request.LoadedPath,
            request.WorkspaceRoot,
            cancellationToken);
        if (hasPendingRecovery)
        {
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(
                "RecoveryPending",
                "Resolve unfinished recovery work before opening this workspace.",
                RequiredAction.ResolveRecovery);
        }

        var loadedWorkspace = await _workspaceLoadWorkflow.LoadAsync(
            request.LoadedPath,
            request.WorkspaceRoot,
            cancellationToken);
        if (loadedWorkspace.HasFailure)
        {
            return CreateLoadFailureResult<WorkspaceOpenOutcome>(loadedWorkspace, "loaded");
        }

        var workspaceId = _sessionStore.AllocateWorkspaceId();
        var instanceStatus = await _instanceStatusPublisher.OpenAsync(
            workspaceId,
            request.WorkspaceRoot,
            request.LoadedPath,
            WorkspaceLifecycleState.Ready,
            cancellationToken);
        WorkspaceInputManifest inputManifest;
        try
        {
            inputManifest = _workspaceChangeDetector.BuildManifest(
                loadedWorkspace.Solution,
                request.LoadedPath);
        }
        catch
        {
            await _instanceStatusPublisher.CloseAsync(workspaceId);
            loadedWorkspace.Workspace.Dispose();
            throw;
        }

        if (!inputManifest.IsComplete)
        {
            await _instanceStatusPublisher.CloseAsync(workspaceId);
            loadedWorkspace.Workspace.Dispose();
            return CreateInputEvaluationFailureResult<WorkspaceOpenOutcome>(inputManifest);
        }

        var session = CreateSessionSnapshot(
            workspaceId,
            request.Alias,
            loadedWorkspace.Workspace,
            loadedWorkspace.Solution,
            request.LoadedPath,
            request.WorkspaceRoot,
            _sessionStore.AllocateWorkspaceEpoch(),
            loadedWorkspace.Diagnostics,
            inputManifest,
            operationGate: null);

        var latestValidationError = TryRegisterSession(session, request.LoadedPath, request.Alias);
        if (latestValidationError is not null)
        {
            await _instanceStatusPublisher.CloseAsync(workspaceId);
            session.LoadedWorkspace.Dispose();
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(latestValidationError);
        }

        return CreateOpenSuccessResult(
            session,
            CreateOpenDiagnostics(session.LoadDiagnostics, instanceStatus));
    }

    public ValueTask<WorkspaceOperationResult<WorkspaceOpenOutcome>> OpenAsync(
        string path,
        string? alias,
        CancellationToken cancellationToken)
    {
        return OpenAsync(path, alias, workspaceRoot: null, cancellationToken);
    }

    public ValueTask<WorkspaceOperationResult<WorkspaceListOutcome>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = _sessionStore.ReadSnapshot();
        return ValueTask.FromResult(_resultFactory.Succeeded(
            new WorkspaceListOutcome
            {
                Workspaces = snapshot.Workspaces.Values
                    .OrderBy(static session => session.Workspace.WorkspaceId, StringComparer.Ordinal)
                    .Select(static session => session.Workspace)
                    .ToArray(),
                TransactionOwnerWorkspaceId = snapshot.TransactionOwnerWorkspaceId,
            }));
    }

    public async ValueTask<WorkspaceOperationResult<WorkspaceCloseOutcome>> CloseAsync(string? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<WorkspaceCloseOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;

        if (session.State is WorkspaceLifecycleState.TransactionActive or WorkspaceLifecycleState.TransactionConflicted)
        {
            return _resultFactory.Rejected<WorkspaceCloseOutcome>(
                "TransactionOpen",
                "Commit or roll back the active transaction before invoking this tool.",
                RequiredAction.CommitOrRollback,
                CreateContext(session));
        }

        var removedSession = _sessionStore.RemoveWorkspace(acquisition.Selection.WorkspaceId);
        if (removedSession is null)
        {
            return _resultFactory.Rejected<WorkspaceCloseOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        await _instanceStatusPublisher.CloseAsync(removedSession.Workspace.WorkspaceId);
        removedSession.LoadedWorkspace.Dispose();
        return _resultFactory.Succeeded(
            new WorkspaceCloseOutcome
            {
                ClosedPath = removedSession.Workspace.LoadedPath,
            },
            CreateContext(removedSession));
    }

    public async ValueTask<WorkspaceOperationResult<WorkspaceStatusOutcome>> GetStatusAsync(
        string? workspaceId,
        string? alias,
        string? path,
        StatusDetailLevel detail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireShared(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<WorkspaceStatusOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;

        if (session.State is WorkspaceLifecycleState.Ready or WorkspaceLifecycleState.TransactionActive
            && _workspaceChangeDetector.HasChanged(session.InputManifest, cancellationToken))
        {
            session = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
            _sessionStore.ReplaceSession(session);
        }

        var instanceStatus = await _instanceStatusPublisher.GetOtherLiveInstancesAsync(
            session.Workspace.WorkspaceRoot,
            cancellationToken);
        return _resultFactory.Succeeded(CreateStatusOutcome(session, detail, instanceStatus), CreateContext(session));
    }

    public async ValueTask<WorkspaceOperationResult<WorkspaceReloadOutcome>> ReloadAsync(string? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<WorkspaceReloadOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var currentSession = acquisition.Session;

        var context = CreateContext(currentSession);
        if (currentSession.State is WorkspaceLifecycleState.TransactionActive or WorkspaceLifecycleState.TransactionConflicted)
        {
            return _resultFactory.Rejected<WorkspaceReloadOutcome>(
                "WorkspaceReloadBlocked",
                "Commit or roll back the active transaction before reloading.",
                RequiredAction.CommitOrRollback,
                context);
        }

        if (currentSession.State != WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return _resultFactory.Rejected<WorkspaceReloadOutcome>(
                "WorkspaceReloadNotRequired",
                "The workspace does not require reload.",
                context: context);
        }

        var loadedWorkspace = await _workspaceLoadWorkflow.LoadAsync(
            currentSession.Workspace.LoadedPath,
            currentSession.Workspace.WorkspaceRoot,
            cancellationToken);
        if (loadedWorkspace.HasFailure)
        {
            return CreateLoadFailureResult<WorkspaceReloadOutcome>(loadedWorkspace, "reloaded", context);
        }

        var inputManifest = _workspaceChangeDetector.BuildManifest(
            loadedWorkspace.Solution,
            currentSession.Workspace.LoadedPath);
        if (!inputManifest.IsComplete)
        {
            loadedWorkspace.Workspace.Dispose();
            return CreateInputEvaluationFailureResult<WorkspaceReloadOutcome>(inputManifest, context);
        }

        var reloadedSession = CreateSessionSnapshot(
            currentSession.Workspace.WorkspaceId,
            currentSession.Workspace.Alias,
            loadedWorkspace.Workspace,
            loadedWorkspace.Solution,
            currentSession.Workspace.LoadedPath,
            currentSession.Workspace.WorkspaceRoot,
            _sessionStore.AllocateWorkspaceEpoch(),
            loadedWorkspace.Diagnostics,
            inputManifest,
            currentSession.OperationGate);

        var oldSession = _sessionStore.ReadSession(acquisition.Selection.WorkspaceId);
        oldSession?.LoadedWorkspace.Dispose();
        _sessionStore.ReplaceSession(reloadedSession);

        return _resultFactory.Succeeded(
            new WorkspaceReloadOutcome
            {
                Workspace = reloadedSession.Workspace,
                ProjectCount = reloadedSession.ProjectCount,
                DocumentCount = reloadedSession.DocumentCount,
                LoadDiagnostics = reloadedSession.LoadDiagnostics,
            },
            CreateContext(reloadedSession));
    }

    private ResolvedWorkspaceOpenRequest ResolveOpenRequest(string path, string? alias, string? workspaceRoot)
    {
        var normalizedPath = _workspaceLoader.NormalizeOpenPath(path);
        if (normalizedPath is null)
        {
            return ResolvedWorkspaceOpenRequest.Failure(new WorkspaceOperationError
            {
                Code = "WorkspacePathInvalid",
                Message = "Workspace paths must be absolute .sln, .slnx, or .csproj files.",
            });
        }

        var resolvedWorkspaceRoot = _workspaceRootResolver.Resolve(normalizedPath, workspaceRoot);
        if (resolvedWorkspaceRoot is null)
        {
            return ResolvedWorkspaceOpenRequest.Failure(new WorkspaceOperationError
            {
                Code = "WorkspaceRootInvalid",
                Message = "The workspace root must be an existing absolute directory containing the loaded path.",
            });
        }

        return ResolvedWorkspaceOpenRequest.Success(
            normalizedPath,
            _workspaceLoader.NormalizeAlias(alias),
            resolvedWorkspaceRoot);
    }

    private WorkspaceOperationError? ValidateOpenPreflight(string loadedPath, string? alias)
    {
        var snapshot = _sessionStore.ReadSnapshot();
        return ValidateOpenCapacity(snapshot)
            ?? ValidateOpenUniqueness(snapshot, loadedPath, alias);
    }

    private async ValueTask<bool> HasPendingRecoveryAsync(
        string loadedPath,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        var statuses = await _recoveryStore.GetStatusesAsync(cancellationToken);
        return statuses.Any(status =>
            (string.IsNullOrWhiteSpace(status.SolutionPath)
                || string.Equals(Path.GetFullPath(status.SolutionPath), loadedPath, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(status.WorkspaceRoot)
                && string.Equals(Path.GetFullPath(status.WorkspaceRoot), workspaceRoot, StringComparison.Ordinal))
            && status.State is not RecoveryState.Committed and not RecoveryState.Restored);
    }

    private WorkspaceOperationError? TryRegisterSession(
        WorkspaceSessionSnapshot session,
        string loadedPath,
        string? alias)
    {
        return _sessionStore.TryAddWorkspace(session, snapshot =>
        {
            return ValidateOpenCapacity(snapshot)
                ?? ValidateOpenUniqueness(snapshot, loadedPath, alias);
        });
    }

    private WorkspaceOperationResult<WorkspaceOpenOutcome> CreateOpenSuccessResult(
        WorkspaceSessionSnapshot session,
        IReadOnlyList<DiagnosticInfo> openDiagnostics)
    {
        return _resultFactory.Succeeded(
            new WorkspaceOpenOutcome
            {
                Workspace = session.Workspace,
                ProjectCount = session.ProjectCount,
                DocumentCount = session.DocumentCount,
                LoadDiagnostics = openDiagnostics,
            },
            CreateContext(session));
    }

    private static IReadOnlyList<DiagnosticInfo> CreateOpenDiagnostics(
        IReadOnlyList<DiagnosticInfo> loadDiagnostics,
        WorkspaceInstanceStatusResult instanceStatus)
    {
        var instanceDiagnostics = CreateInstanceDiagnostics(instanceStatus);
        return instanceDiagnostics.Count == 0
            ? loadDiagnostics
            : loadDiagnostics.Concat(instanceDiagnostics).ToArray();
    }

    private WorkspaceSessionSnapshot CreateSessionSnapshot(
        string workspaceId,
        string? alias,
        ILoadedWorkspace workspace,
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        long workspaceEpoch,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        WorkspaceInputManifest inputManifest,
        IWorkspaceOperationGate? operationGate)
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
                WorkspaceRoot = workspaceRoot,
            },
            LoadedWorkspace = workspace,
            CurrentSolution = solution,
            Transaction = null,
            ProjectCount = solution.Projects.Count(),
            DocumentCount = solution.Projects.Sum(static project => project.Documents.Count()),
            LoadDiagnostics = diagnostics,
            InputManifest = inputManifest,
            OperationGate = operationGate ?? new WorkspaceOperationGate(_options.MaxConcurrentQueries),
        };
    }

    private static WorkspaceStatusOutcome CreateStatusOutcome(
        WorkspaceSessionSnapshot session,
        StatusDetailLevel detail,
        WorkspaceInstanceStatusResult instanceStatus)
    {
        return new WorkspaceStatusOutcome
        {
            State = session.State,
            Workspace = session.Workspace,
            ProjectCount = session.ProjectCount,
            DocumentCount = session.DocumentCount,
            LoadDiagnostics = CreateStatusDiagnostics(session.LoadDiagnostics, detail, instanceStatus),
            Transaction = session.Transaction?.ToInfo(session.State == WorkspaceLifecycleState.TransactionConflicted),
            ReloadRequired = session.State == WorkspaceLifecycleState.WorkspaceOutOfDate,
            Instances = instanceStatus.Instances,
        };
    }

    private static IReadOnlyList<DiagnosticInfo>? CreateStatusDiagnostics(
        IReadOnlyList<DiagnosticInfo> loadDiagnostics,
        StatusDetailLevel detail,
        WorkspaceInstanceStatusResult instanceStatus)
    {
        var instanceDiagnostics = CreateInstanceDiagnostics(instanceStatus);
        if (detail != StatusDetailLevel.Full && instanceDiagnostics.Count == 0)
        {
            return null;
        }

        return detail == StatusDetailLevel.Full
            ? loadDiagnostics.Concat(instanceDiagnostics).ToArray()
            : instanceDiagnostics;
    }

    private static IReadOnlyList<DiagnosticInfo> CreateInstanceDiagnostics(WorkspaceInstanceStatusResult instanceStatus)
    {
        var diagnostics = new List<DiagnosticInfo>();
        if (!instanceStatus.IsAvailable)
        {
            diagnostics.Add(new DiagnosticInfo
            {
                Id = "WorkspaceInstanceStatusUnavailable",
                Severity = Contracts.Results.DiagnosticSeverity.Warning,
                Message = "Advisory workspace-instance status could not be published or queried. Treat this workspace as potentially in use: remain query-only, use it only when necessary, and expect query results to become stale. Coordinate mutation ownership before starting a transaction.",
            });
        }

        if (instanceStatus.HasOtherLiveInstance)
        {
            diagnostics.Add(new DiagnosticInfo
            {
                Id = "WorkspaceInUse",
                Severity = Contracts.Results.DiagnosticSeverity.Warning,
                Message = "Another Roslyn Workbench MCP instance has this workspace open. Treat this workspace as query-only, use it only when necessary, and expect query results to become stale. Coordinate mutation ownership before starting a transaction.",
            });
        }

        if (instanceStatus.HasUnreadableLiveInstance)
        {
            diagnostics.Add(new DiagnosticInfo
            {
                Id = "WorkspaceInstanceStatusUnreadable",
                Severity = Contracts.Results.DiagnosticSeverity.Warning,
                Message = "One or more live workspace-instance status files could not be read or validated. Treat this workspace as potentially in use: remain query-only, use it only when necessary, and expect query results to become stale. Coordinate mutation ownership before starting a transaction.",
            });
        }

        return diagnostics.ToArray();
    }

    private WorkspaceOperationError? ValidateOpenCapacity(WorkspaceHostSnapshot hostSnapshot)
    {
        return hostSnapshot.Workspaces.Count >= _options.MaxLoadedWorkspaces
            ? new WorkspaceOperationError
            {
                Code = WorkspaceErrorCodes.WorkspaceCapacityReached,
                Message = "Close an existing workspace before opening another one.",
            }
            : null;
    }

    private static WorkspaceOperationError? ValidateOpenUniqueness(WorkspaceHostSnapshot hostSnapshot, string normalizedPath, string? alias)
    {
        if (hostSnapshot.Workspaces.Values.Any(session => string.Equals(session.Workspace.LoadedPath, normalizedPath, StringComparison.Ordinal)))
        {
            return new WorkspaceOperationError
            {
                Code = WorkspaceErrorCodes.WorkspaceAlreadyOpen,
                Message = "A workspace for this path is already open.",
            };
        }

        if (alias is not null
            && hostSnapshot.Workspaces.Values.Any(session => string.Equals(session.Workspace.Alias, alias, StringComparison.Ordinal)))
        {
            return new WorkspaceOperationError
            {
                Code = WorkspaceErrorCodes.WorkspaceAlreadyOpen,
                Message = "A workspace with this alias is already open.",
            };
        }

        return null;
    }

    private static Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors.WorkspaceSelector? CreateWorkspaceSelector(string? workspaceId, string? alias, string? path)
    {
        return workspaceId is null && alias is null && path is null
            ? null
            : new Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors.WorkspaceSelector
            {
                WorkspaceId = workspaceId,
                Alias = alias,
                Path = path,
            };
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

    private WorkspaceOperationResult<TOutcome> CreateAcquisitionFailureResult<TOutcome>(
        WorkspaceSessionAcquisition acquisition,
        WorkspaceOperationError error)
    {
        var context = acquisition.ContextSession is null ? null : CreateContext(acquisition.ContextSession);
        return _resultFactory.Rejected<TOutcome>(error, context);
    }

    private WorkspaceOperationResult<TOutcome> CreateLoadFailureResult<TOutcome>(
        ValidatedWorkspaceLoadResult loadResult,
        string operation,
        WorkspaceOperationContext? context = null)
    {
        return loadResult.Failure switch
        {
            ValidatedWorkspaceLoadFailure.LoadFailed => _resultFactory.Rejected<TOutcome>(
                WorkspaceErrorCodes.WorkspaceLoadFailed,
                $"The workspace could not be {operation}.",
                context: context,
                diagnostics: loadResult.Diagnostics),
            ValidatedWorkspaceLoadFailure.NotSupported => _resultFactory.Rejected<TOutcome>(
                WorkspaceErrorCodes.WorkspaceNotSupported,
                "Only SDK-style C# projects are supported.",
                context: context),
            ValidatedWorkspaceLoadFailure.OutsideWorkspaceRoot => _resultFactory.Rejected<TOutcome>(
                "WorkspaceProjectOutsideRoot",
                "Every loaded project must be contained by the workspace root.",
                context: context),
            _ => throw new InvalidOperationException("The workspace load failure is not supported."),
        };
    }

    private WorkspaceOperationResult<TOutcome> CreateInputEvaluationFailureResult<TOutcome>(
        WorkspaceInputManifest manifest,
        WorkspaceOperationContext? context = null)
    {
        return _resultFactory.Faulted<TOutcome>(
            "WorkspaceInputEvaluationFailed",
            "The workspace inputs could not be evaluated safely.",
            RequiredAction.Retry,
            context,
            WorkspaceInputEvaluationDiagnostics.Create(manifest.EvaluationFailures));
    }

    private static void DisposeFailedAcquisition(WorkspaceSessionAcquisition acquisition)
    {
        acquisition.Lease?.Dispose();
    }
}
