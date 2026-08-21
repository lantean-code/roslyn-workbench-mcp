using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

internal sealed class WorkspaceLifecycleService : IWorkspaceLifecycleService
{
    private readonly WorkspaceOptions _options;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSessionAcquirer _sessionAcquirer;
    private readonly IWorkspaceLoader _workspaceLoader;
    private readonly IWorkspaceMsBuildPropertiesResolver _msBuildPropertiesResolver;
    private readonly IWorkspaceRootResolver _workspaceRootResolver;
    private readonly IWorkspacePathComparison _workspacePathComparison;
    private readonly IWorkspacePathNormalizer _workspacePathNormalizer;
    private readonly IWorkspaceLoadWorkflow _workspaceLoadWorkflow;
    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceReadOnlyDocumentValidator _readOnlyDocumentValidator;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;
    private readonly IWorkspaceSessionCleanup _sessionCleanup;

    public WorkspaceLifecycleService(
        IOptions<WorkspaceOptions> options,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceSessionAcquirer sessionAcquirer,
        IWorkspaceLoader workspaceLoader,
        IWorkspaceMsBuildPropertiesResolver msBuildPropertiesResolver,
        IWorkspaceRootResolver workspaceRootResolver,
        IWorkspacePathComparison workspacePathComparison,
        IWorkspacePathNormalizer workspacePathNormalizer,
        IWorkspaceLoadWorkflow workspaceLoadWorkflow,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceReadOnlyDocumentValidator readOnlyDocumentValidator,
        IWorkspaceStateTransitions workspaceStateTransitions,
        IWorkspaceOperationResultFactory resultFactory,
        ICommitRecoveryStore recoveryStore,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher,
        IWorkspaceSessionCleanup sessionCleanup)
    {
        _options = options.Value;
        _sessionStore = sessionStore;
        _sessionAcquirer = sessionAcquirer;
        _workspaceLoader = workspaceLoader;
        _msBuildPropertiesResolver = msBuildPropertiesResolver;
        _workspaceRootResolver = workspaceRootResolver;
        _workspacePathComparison = workspacePathComparison;
        _workspacePathNormalizer = workspacePathNormalizer;
        _workspaceLoadWorkflow = workspaceLoadWorkflow;
        _workspaceChangeDetector = workspaceChangeDetector;
        _readOnlyDocumentValidator = readOnlyDocumentValidator;
        _workspaceStateTransitions = workspaceStateTransitions;
        _resultFactory = resultFactory;
        _recoveryStore = recoveryStore;
        _instanceStatusPublisher = instanceStatusPublisher;
        _sessionCleanup = sessionCleanup;
    }

    public async ValueTask<WorkspaceOperationResult<WorkspaceOpenOutcome>> OpenAsync(
        string path,
        string? alias,
        string? workspaceRoot,
        WorkspaceMsBuildProperties? msBuildProperties,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = ResolveOpenRequest(path, alias, workspaceRoot, msBuildProperties);
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
            cancellationToken);

        if (hasPendingRecovery)
        {
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(
                "RecoveryPending",
                "Resolve unfinished recovery work before opening this workspace.",
                RequiredAction.ResolveRecovery);
        }

        using var inputCertification = _workspaceChangeDetector.BeginCertification(request.WorkspaceRoot);
        ValidatedWorkspaceLoadResult loadedWorkspace;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            "workspace-open",
            WorkbenchPerformanceEventSource.WorkspaceLoadPhase))
        {
            loadedWorkspace = await _workspaceLoadWorkflow.LoadAsync(
                request.LoadedPath,
                request.WorkspaceRoot,
                request.MsBuildProperties,
                cancellationToken);
        }

        if (loadedWorkspace.HasFailure)
        {
            return CreateLoadFailureResult<WorkspaceOpenOutcome>(loadedWorkspace, "loaded");
        }

        var workspaceId = _sessionStore.AllocateWorkspaceId();
        WorkspaceInputManifest? inputManifest = null;
        var sessionRegistered = false;
        try
        {
            var instanceStatus = await _instanceStatusPublisher.OpenAsync(
                workspaceId,
                request.WorkspaceRoot,
                request.LoadedPath,
                WorkspaceLifecycleState.Ready,
                cancellationToken);

            inputManifest = _workspaceChangeDetector.BuildManifest(
                loadedWorkspace.Solution,
                request.LoadedPath,
                request.WorkspaceRoot,
                inputCertification,
                request.MsBuildProperties);

            if (!inputManifest.IsComplete)
            {
                return CreateInputEvaluationFailureResult<WorkspaceOpenOutcome>(inputManifest);
            }

            var readOnlyDocumentValidation = await _readOnlyDocumentValidator.ValidateAsync(
                loadedWorkspace.Solution,
                request.WorkspaceRoot,
                cancellationToken);

            if (readOnlyDocumentValidation == WorkspaceReadOnlyDocumentValidationStatus.Invalid)
            {
                return CreateInputCertificationFailureResult<WorkspaceOpenOutcome>();
            }

            var workspaceInputsChanged = _workspaceChangeDetector.HasChanged(
                inputManifest,
                cancellationToken);

            if (workspaceInputsChanged)
            {
                return CreateInputCertificationFailureResult<WorkspaceOpenOutcome>();
            }

            var session = CreateSessionSnapshot(
                workspaceId,
                request.Alias,
                loadedWorkspace.Workspace,
                loadedWorkspace.Solution,
                loadedWorkspace.ProjectTargetFrameworks,
                request.LoadedPath,
                request.WorkspaceRoot,
                request.MsBuildProperties,
                _sessionStore.AllocateWorkspaceEpoch(),
                loadedWorkspace.Diagnostics,
                inputManifest,
                operationGate: null);

            var latestValidationError = TryRegisterSession(session, request.LoadedPath, request.Alias);
            if (latestValidationError is not null)
            {
                return _resultFactory.Rejected<WorkspaceOpenOutcome>(latestValidationError);
            }

            sessionRegistered = true;
            var openDiagnostics = CreateOpenDiagnostics(
                session.LoadDiagnostics,
                instanceStatus,
                request.LoadedPath);

            return CreateOpenSuccessResult(session, openDiagnostics);
        }
        finally
        {
            if (!sessionRegistered)
            {
                try
                {
                    await _instanceStatusPublisher.CloseAsync(workspaceId);
                }
                finally
                {
                    try
                    {
                        inputManifest?.Dispose();
                    }
                    finally
                    {
                        loadedWorkspace.Workspace.Dispose();
                    }
                }
            }
        }
    }

    public ValueTask<WorkspaceOperationResult<WorkspaceListOutcome>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = _sessionStore.ReadSnapshot();
        var workspaces = snapshot.Workspaces.Values
            .OrderBy(static session => session.Workspace.WorkspaceId)
            .Select(static session => session.Workspace)
            .ToArray();

        var outcome = new WorkspaceListOutcome
        {
            Workspaces = workspaces,
            TransactionOwnerWorkspaceId = snapshot.TransactionOwnerWorkspaceId,
        };

        var result = _resultFactory.Succeeded(outcome);

        return ValueTask.FromResult(result);
    }

    public async ValueTask<WorkspaceOperationResult<WorkspaceCloseOutcome>> CloseAsync(Guid? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
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
            var rejectionContext = WorkspaceOperationContextFactory.Create(session);

            return _resultFactory.Rejected<WorkspaceCloseOutcome>(
                "TransactionOpen",
                "Commit or roll back the active transaction before invoking this tool.",
                RequiredAction.CommitOrRollback,
                rejectionContext);
        }

        var failureContext = WorkspaceFailureContextFactory.Create(session);
        var removedSession = _sessionStore.RemoveWorkspace(acquisition.Selection.WorkspaceId);
        if (removedSession is null)
        {
            return _resultFactory.Rejected<WorkspaceCloseOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        await CleanupClosedSessionAsync(removedSession, failureContext);

        var operationContext = WorkspaceOperationContextFactory.Create(removedSession);

        var outcome = new WorkspaceCloseOutcome
        {
            ClosedPath = removedSession.Workspace.LoadedPath,
        };

        return _resultFactory.Succeeded(outcome, operationContext);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Application shutdown must release every open workspace before reporting any cleanup failure.")]
    public async ValueTask ShutdownAsync()
    {
        var sessions = _sessionStore.DrainWorkspaces();
        List<Exception>? failures = null;
        foreach (var session in sessions)
        {
            try
            {
                await _sessionCleanup.CleanupAsync(session);
            }
            catch (Exception exception)
            {
                failures ??= [];
                failures.Add(exception);
            }
        }

        if (failures is null)
        {
            return;
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException(
            "One or more failures occurred while shutting down open workspaces.",
            failures);
    }

    public async ValueTask<WorkspaceOperationResult<WorkspaceStatusOutcome>> GetStatusAsync(
        Guid? workspaceId,
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
            _instanceStatusPublisher.QueueUpdate(
                session.Workspace.WorkspaceId,
                session.State,
                session.Transaction?.CurrentRevision,
                commitId: null,
                commitPhase: null);
        }

        var instanceStatus = await _instanceStatusPublisher.GetOtherLiveInstancesAsync(
            session.Workspace.WorkspaceRoot,
            cancellationToken);

        var outcome = CreateStatusOutcome(session, detail, instanceStatus);
        var context = WorkspaceOperationContextFactory.Create(session);
        return _resultFactory.Succeeded(outcome, context);
    }

    public async ValueTask<WorkspaceOperationResult<WorkspaceReloadOutcome>> ReloadAsync(Guid? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
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

        var context = WorkspaceOperationContextFactory.Create(currentSession);
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

        using var inputCertification = _workspaceChangeDetector.BeginCertification(
            currentSession.Workspace.WorkspaceRoot);
        var loadedWorkspace = await _workspaceLoadWorkflow.LoadAsync(
            currentSession.Workspace.LoadedPath,
            currentSession.Workspace.WorkspaceRoot,
            currentSession.MsBuildProperties,
            cancellationToken);

        if (loadedWorkspace.HasFailure)
        {
            return CreateLoadFailureResult<WorkspaceReloadOutcome>(loadedWorkspace, "reloaded", context);
        }

        WorkspaceInputManifest? inputManifest = null;
        try
        {
            inputManifest = _workspaceChangeDetector.BuildManifest(
                loadedWorkspace.Solution,
                currentSession.Workspace.LoadedPath,
                currentSession.Workspace.WorkspaceRoot,
                inputCertification,
                currentSession.MsBuildProperties);
            if (!inputManifest.IsComplete)
            {
                inputManifest.Dispose();
                loadedWorkspace.Workspace.Dispose();
                return CreateInputEvaluationFailureResult<WorkspaceReloadOutcome>(inputManifest, context);
            }

            var readOnlyDocumentValidation = await _readOnlyDocumentValidator.ValidateAsync(
                loadedWorkspace.Solution,
                currentSession.Workspace.WorkspaceRoot,
                cancellationToken);

            if (readOnlyDocumentValidation == WorkspaceReadOnlyDocumentValidationStatus.Invalid)
            {
                inputManifest.Dispose();
                loadedWorkspace.Workspace.Dispose();
                return CreateInputCertificationFailureResult<WorkspaceReloadOutcome>(context);
            }

            var workspaceInputsChanged = _workspaceChangeDetector.HasChanged(
                inputManifest,
                cancellationToken);

            if (workspaceInputsChanged)
            {
                inputManifest.Dispose();
                loadedWorkspace.Workspace.Dispose();
                return CreateInputCertificationFailureResult<WorkspaceReloadOutcome>(context);
            }
        }
        catch
        {
            inputManifest?.Dispose();
            loadedWorkspace.Workspace.Dispose();
            throw;
        }

        var reloadedSession = CreateSessionSnapshot(
            currentSession.Workspace.WorkspaceId,
            currentSession.Workspace.Alias,
            loadedWorkspace.Workspace,
            loadedWorkspace.Solution,
            loadedWorkspace.ProjectTargetFrameworks,
            currentSession.Workspace.LoadedPath,
            currentSession.Workspace.WorkspaceRoot,
            currentSession.MsBuildProperties,
            _sessionStore.AllocateWorkspaceEpoch(),
            loadedWorkspace.Diagnostics,
            inputManifest,
            currentSession.OperationGate);

        var oldSession = _sessionStore.ReadSession(acquisition.Selection.WorkspaceId);
        WorkspaceFailureContext? replacedFailureContext = null;
        if (oldSession is not null)
        {
            replacedFailureContext = WorkspaceFailureContextFactory.Create(oldSession);
        }

        _sessionStore.ReplaceSession(reloadedSession);

        if (oldSession is not null && replacedFailureContext is not null)
        {
            DisposeReplacedSession(oldSession, replacedFailureContext);
        }

        var reloadedContext = WorkspaceOperationContextFactory.Create(reloadedSession);
        var reloadedFailureContext = WorkspaceFailureContextFactory.Create(reloadedSession);

        await UpdateReloadedStatusAsync(
            reloadedSession.Workspace.WorkspaceId,
            reloadedSession.State,
            reloadedFailureContext);

        var outcome = new WorkspaceReloadOutcome
        {
            Workspace = reloadedSession.Workspace,
            ProjectCount = reloadedSession.ProjectCount,
            DocumentCount = reloadedSession.DocumentCount,
            LoadDiagnostics = reloadedSession.LoadDiagnostics,
        };

        return _resultFactory.Succeeded(outcome, reloadedContext);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Unexpected cleanup failures must retain the immutable context captured before the Workspace session is removed.")]
    private async ValueTask CleanupClosedSessionAsync(
        WorkspaceSessionSnapshot removedSession,
        WorkspaceFailureContext context)
    {
        try
        {
            await _sessionCleanup.CleanupAsync(removedSession);
        }
        catch (Exception exception)
        {
            throw new WorkspaceOperationException(context, exception);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Unexpected disposal failures must retain the immutable context of the Workspace session whose resources are being released.")]
    private static void DisposeReplacedSession(
        WorkspaceSessionSnapshot replacedSession,
        WorkspaceFailureContext context)
    {
        try
        {
            replacedSession.InputManifest.Dispose();
            replacedSession.LoadedWorkspace.Dispose();
        }
        catch (Exception exception)
        {
            throw new WorkspaceOperationException(context, exception);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Unexpected status publication failures must retain the immutable context of the newly installed Workspace session.")]
    private async ValueTask UpdateReloadedStatusAsync(
        Guid workspaceId,
        WorkspaceLifecycleState state,
        WorkspaceFailureContext context)
    {
        try
        {
            await _instanceStatusPublisher.UpdateAsync(
                workspaceId,
                state,
                transactionRevision: null,
                commitId: null,
                commitPhase: null);
        }
        catch (Exception exception)
        {
            throw new WorkspaceOperationException(context, exception);
        }
    }

    private ResolvedWorkspaceOpenRequest ResolveOpenRequest(
        string path,
        string? alias,
        string? workspaceRoot,
        WorkspaceMsBuildProperties? msBuildProperties)
    {
        var normalizedPath = _workspaceLoader.NormalizeOpenPath(path);
        if (normalizedPath is null)
        {
            var error = new WorkspaceOperationError
            {
                Code = "WorkspacePathInvalid",
                Message = "Workspace paths must be absolute .sln, .slnx, or .csproj files.",
            };

            return ResolvedWorkspaceOpenRequest.Failure(error);
        }

        var resolvedWorkspaceRoot = _workspaceRootResolver.Resolve(normalizedPath, workspaceRoot);
        if (resolvedWorkspaceRoot is null)
        {
            var error = new WorkspaceOperationError
            {
                Code = "WorkspaceRootInvalid",
                Message = "The workspace root must be an existing absolute directory containing the loaded path.",
            };

            return ResolvedWorkspaceOpenRequest.Failure(error);
        }

        var msBuildPropertiesResolution = _msBuildPropertiesResolver.Resolve(msBuildProperties);
        if (msBuildPropertiesResolution.HasError)
        {
            return ResolvedWorkspaceOpenRequest.Failure(msBuildPropertiesResolution.Error);
        }

        return ResolvedWorkspaceOpenRequest.Success(
            normalizedPath,
            _workspaceLoader.NormalizeAlias(alias),
            resolvedWorkspaceRoot,
            msBuildPropertiesResolution.Properties);
    }

    private WorkspaceOperationError? ValidateOpenPreflight(string loadedPath, string? alias)
    {
        var snapshot = _sessionStore.ReadSnapshot();
        var capacityError = ValidateOpenCapacity(snapshot);
        if (capacityError is not null)
        {
            return capacityError;
        }

        return ValidateOpenUniqueness(snapshot, loadedPath, alias);
    }

    private async ValueTask<bool> HasPendingRecoveryAsync(
        string loadedPath,
        CancellationToken cancellationToken)
    {
        var statuses = await _recoveryStore.GetStatusesAsync(cancellationToken);
        foreach (var status in statuses)
        {
            if (status.State is RecoveryState.Committed or RecoveryState.Restored)
            {
                continue;
            }

            if (IsPendingRecoveryForWorkspace(status, loadedPath))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPendingRecoveryForWorkspace(RecoveryStatus status, string loadedPath)
    {
        if (status.HasMalformedWorkspaceIdentity)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(status.SolutionPath))
        {
            return true;
        }

        if (!_workspacePathNormalizer.TryGetFullPath(status.SolutionPath, out var recoveredLoadedPath))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(status.WorkspaceRoot)
            && !_workspacePathNormalizer.TryGetFullPath(status.WorkspaceRoot, out _))
        {
            return true;
        }

        return PathsEqual(recoveredLoadedPath, loadedPath);
    }

    private WorkspaceOperationError? TryRegisterSession(
        WorkspaceSessionSnapshot session,
        string loadedPath,
        string? alias)
    {
        return _sessionStore.TryAddWorkspace(session, snapshot =>
        {
            var capacityError = ValidateOpenCapacity(snapshot);
            if (capacityError is not null)
            {
                return capacityError;
            }

            return ValidateOpenUniqueness(snapshot, loadedPath, alias);
        });
    }

    private WorkspaceOperationResult<WorkspaceOpenOutcome> CreateOpenSuccessResult(
        WorkspaceSessionSnapshot session,
        IReadOnlyList<DiagnosticInfo> openDiagnostics)
    {
        var outcome = new WorkspaceOpenOutcome
        {
            Workspace = session.Workspace,
            ProjectCount = session.ProjectCount,
            DocumentCount = session.DocumentCount,
            LoadDiagnostics = openDiagnostics,
        };

        var context = WorkspaceOperationContextFactory.Create(session);

        return _resultFactory.Succeeded(outcome, context);
    }

    private IReadOnlyList<DiagnosticInfo> CreateOpenDiagnostics(
        IReadOnlyList<DiagnosticInfo> loadDiagnostics,
        WorkspaceInstanceStatusResult instanceStatus,
        string loadedPath)
    {
        var instanceDiagnostics = CreateInstanceDiagnostics(instanceStatus);
        var storageDiagnostics = CreateStorageDiagnostics(loadedPath);
        if (instanceDiagnostics.Length == 0 && storageDiagnostics.Length == 0)
        {
            return loadDiagnostics;
        }

        return loadDiagnostics
            .Concat(instanceDiagnostics)
            .Concat(storageDiagnostics)
            .ToArray();
    }

    private WorkspaceSessionSnapshot CreateSessionSnapshot(
        Guid workspaceId,
        string? alias,
        ILoadedWorkspace workspace,
        Solution solution,
        WorkspaceProjectTargetFrameworkMap projectTargetFrameworks,
        string loadedPath,
        string workspaceRoot,
        WorkspaceMsBuildProperties? msBuildProperties,
        long workspaceEpoch,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        WorkspaceInputManifest inputManifest,
        IWorkspaceOperationGate? operationGate)
    {
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = workspaceId,
            Alias = alias,
            WorkspaceEpoch = workspaceEpoch,
            LoadedPath = loadedPath,
            WorkspaceRoot = workspaceRoot,
        };

        var committedSnapshotId = _sessionStore.AllocateWorkspaceSnapshotId();
        var snapshotIdentity = WorkspaceSnapshotIdentity.Create(
            workspaceIdentity,
            committedSnapshotId,
            transaction: null);

        var effectiveOperationGate = operationGate;
        effectiveOperationGate ??= new WorkspaceOperationGate(_options.MaxConcurrentQueries);

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = committedSnapshotId,
            State = WorkspaceLifecycleState.Ready,
            Workspace = workspaceIdentity,
            LoadedWorkspace = workspace,
            CurrentSolution = solution,
            ProjectTargetFrameworks = projectTargetFrameworks,
            MsBuildProperties = msBuildProperties,
            Transaction = null,
            ProjectCount = solution.Projects.Count(),
            DocumentCount = solution.Projects.Sum(static project => project.Documents.Count()),
            LoadDiagnostics = diagnostics,
            InputManifest = inputManifest,
            OperationGate = effectiveOperationGate,
            CurrentSnapshotIdentity = snapshotIdentity,
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
            ExternalChange = session.InputManifest.Change,
            Instances = instanceStatus.Instances,
        };
    }

    private static DiagnosticInfo[]? CreateStatusDiagnostics(
        IReadOnlyList<DiagnosticInfo> loadDiagnostics,
        StatusDetailLevel detail,
        WorkspaceInstanceStatusResult instanceStatus)
    {
        var instanceDiagnostics = CreateInstanceDiagnostics(instanceStatus);
        if (detail != StatusDetailLevel.Full && instanceDiagnostics.Length == 0)
        {
            return null;
        }

        if (detail != StatusDetailLevel.Full)
        {
            return instanceDiagnostics;
        }

        return loadDiagnostics.Concat(instanceDiagnostics).ToArray();
    }

    private static DiagnosticInfo[] CreateInstanceDiagnostics(WorkspaceInstanceStatusResult instanceStatus)
    {
        var diagnostics = new List<DiagnosticInfo>();
        if (!instanceStatus.IsAvailable)
        {
            diagnostics.Add(new DiagnosticInfo
            {
                Id = "WorkspaceInstanceStatusUnavailable",
                Severity = Results.DiagnosticSeverity.Warning,
                Message = "Advisory workspace-instance status could not be published or queried. Treat this workspace as potentially in use: remain query-only, use it only when necessary, and expect query results to become stale. Coordinate mutation ownership before starting a transaction.",
            });
        }

        if (instanceStatus.HasOtherLiveInstance)
        {
            diagnostics.Add(new DiagnosticInfo
            {
                Id = "WorkspaceInUse",
                Severity = Results.DiagnosticSeverity.Warning,
                Message = "Another Roslyn Workbench MCP instance has this workspace open. Treat this workspace as query-only, use it only when necessary, and expect query results to become stale. Coordinate mutation ownership before starting a transaction.",
            });
        }

        if (instanceStatus.HasUnreadableLiveInstance)
        {
            diagnostics.Add(new DiagnosticInfo
            {
                Id = "WorkspaceInstanceStatusUnreadable",
                Severity = Results.DiagnosticSeverity.Warning,
                Message = "One or more live workspace-instance status files could not be read or validated. Treat this workspace as potentially in use: remain query-only, use it only when necessary, and expect query results to become stale. Coordinate mutation ownership before starting a transaction.",
            });
        }

        return diagnostics.ToArray();
    }

    private DiagnosticInfo[] CreateStorageDiagnostics(string loadedPath)
    {
        if (!_workspacePathComparison.IsWindowsFileSystemPath(loadedPath))
        {
            return [];
        }

        return
        [
            new DiagnosticInfo
            {
                Id = "WorkspaceOnWindowsFileSystemFromWsl",
                Severity = Results.DiagnosticSeverity.Warning,
                Message = "This workspace is being accessed from WSL through the Windows file system, which can substantially reduce workspace and query performance. For better performance, place the repository on the WSL file system or run Roslyn Workbench MCP directly on Windows.",
            },
        ];
    }

    private WorkspaceOperationError? ValidateOpenCapacity(WorkspaceHostSnapshot hostSnapshot)
    {
        if (hostSnapshot.Workspaces.Count < _options.MaxLoadedWorkspaces)
        {
            return null;
        }

        return new WorkspaceOperationError
        {
            Code = WorkspaceErrorCodes.WorkspaceCapacityReached,
            Message = "Close an existing workspace before opening another one.",
        };
    }

    private WorkspaceOperationError? ValidateOpenUniqueness(WorkspaceHostSnapshot hostSnapshot, string normalizedPath, string? alias)
    {
        if (hostSnapshot.Workspaces.Values.Any(session => PathsEqual(session.Workspace.LoadedPath, normalizedPath)))
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

    private bool PathsEqual(string first, string second)
    {
        return string.Equals(first, second, _workspacePathComparison.GetComparison(first));
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
                "The workspace does not contain any supported SDK-style C# projects.",
                context: context,
                diagnostics: loadResult.Diagnostics),
            ValidatedWorkspaceLoadFailure.OutsideWorkspaceRoot => _resultFactory.Rejected<TOutcome>(
                "WorkspaceProjectOutsideRoot",
                "Every loaded project must be contained by the workspace root.",
                context: context,
                diagnostics: loadResult.Diagnostics),
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

    private WorkspaceOperationResult<TOutcome> CreateInputCertificationFailureResult<TOutcome>(
        WorkspaceOperationContext? context = null)
    {
        return _resultFactory.Rejected<TOutcome>(
            "WorkspaceChangedDuringLoad",
            "Workspace inputs changed while the workspace was being loaded. Retry after the files have stabilised.",
            RequiredAction.Retry,
            context);
    }

    private static void DisposeFailedAcquisition(WorkspaceSessionAcquisition acquisition)
    {
        acquisition.Lease?.Dispose();
    }
}
