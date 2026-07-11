using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

internal sealed class WorkspaceLifecycleService : IWorkspaceLifecycleService
{
    private readonly WorkspaceCoordinatorOptions _options;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSelector _workspaceSelector;
    private readonly IWorkspaceLoader _workspaceLoader;
    private readonly IWorkspaceRootResolver _workspaceRootResolver;
    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;

    public WorkspaceLifecycleService(
        IOptions<WorkspaceCoordinatorOptions> options,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceSelector workspaceSelector,
        IWorkspaceLoader workspaceLoader,
        IWorkspaceRootResolver workspaceRootResolver,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        IWorkspaceOperationResultFactory resultFactory,
        ICommitRecoveryStore recoveryStore,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher)
    {
        _options = options.Value;
        _sessionStore = sessionStore;
        _workspaceSelector = workspaceSelector;
        _workspaceLoader = workspaceLoader;
        _workspaceRootResolver = workspaceRootResolver;
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

        var normalizedPath = _workspaceLoader.NormalizeOpenPath(path);
        if (normalizedPath is null)
        {
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(
                "WorkspacePathInvalid",
                "Workspace paths must be absolute .sln, .slnx, or .csproj files.");
        }

        var resolvedWorkspaceRoot = _workspaceRootResolver.Resolve(normalizedPath, workspaceRoot);
        if (resolvedWorkspaceRoot is null)
        {
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(
                "WorkspaceRootInvalid",
                "The workspace root must be an existing absolute directory containing the loaded path.");
        }

        var normalizedAlias = _workspaceLoader.NormalizeAlias(alias);
        var preflightSnapshot = _sessionStore.ReadSnapshot();
        var capacityError = ValidateOpenCapacity(preflightSnapshot);
        if (capacityError is not null)
        {
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(capacityError);
        }

        var uniquenessError = ValidateOpenUniqueness(preflightSnapshot, normalizedPath, normalizedAlias);
        if (uniquenessError is not null)
        {
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(uniquenessError);
        }

        if ((await _recoveryStore.GetStatusesAsync(cancellationToken).ConfigureAwait(false)).Any(status =>
                (string.IsNullOrWhiteSpace(status.SolutionPath)
                    || string.Equals(Path.GetFullPath(status.SolutionPath), normalizedPath, StringComparison.Ordinal)
                    || !string.IsNullOrWhiteSpace(status.WorkspaceRoot)
                    && string.Equals(Path.GetFullPath(status.WorkspaceRoot), resolvedWorkspaceRoot, StringComparison.Ordinal))
                && status.State is not RecoveryState.Committed and not RecoveryState.Restored))
        {
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(
                "RecoveryPending",
                "Resolve unfinished recovery work before opening this workspace.",
                RequiredAction.ResolveRecovery);
        }

        if (string.Equals(Path.GetExtension(normalizedPath), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var preflight = _workspaceLoader.InspectCompatibility(normalizedPath);
            if (preflight.Diagnostics.Count > 0)
            {
                return _resultFactory.Rejected<WorkspaceOpenOutcome>(
                    WorkspaceErrorCodes.WorkspaceLoadFailed,
                    "The workspace could not be loaded.",
                    diagnostics: preflight.Diagnostics);
            }

            if (!preflight.IsSdkStyle)
            {
                return _resultFactory.Rejected<WorkspaceOpenOutcome>(
                    WorkspaceErrorCodes.WorkspaceNotSupported,
                    "Only SDK-style C# projects are supported.");
            }
        }

        var loadedWorkspace = await _workspaceLoader.LoadAsync(normalizedPath, cancellationToken).ConfigureAwait(false);
        if (loadedWorkspace.Solution is null || loadedWorkspace.Workspace is null)
        {
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(
                WorkspaceErrorCodes.WorkspaceLoadFailed,
                "The workspace could not be loaded.",
                diagnostics: loadedWorkspace.Diagnostics);
        }


        var loadedInputPaths = loadedWorkspace.Solution.Projects
            .SelectMany(static project => project.Documents
                .Select(static document => document.FilePath)
                .Prepend(project.FilePath))
            .Where(static inputPath => !string.IsNullOrWhiteSpace(inputPath));
        if (loadedInputPaths.Any(inputPath => !_workspaceRootResolver.Contains(resolvedWorkspaceRoot, inputPath!)))
        {
            loadedWorkspace.Workspace.Dispose();
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(
                "WorkspaceProjectOutsideRoot",
                "Every loaded project must be contained by the workspace root.");
        }

        var session = CreateSessionSnapshot(
            _sessionStore.AllocateWorkspaceId(),
            normalizedAlias,
            loadedWorkspace.Workspace,
            loadedWorkspace.Solution,
            normalizedPath,
            resolvedWorkspaceRoot,
            _sessionStore.AllocateWorkspaceEpoch(),
            loadedWorkspace.Diagnostics,
            operationGate: null);

        foreach (var project in session.CurrentSolution.Projects
                     .Where(static project => string.Equals(project.Language, LanguageNames.CSharp, StringComparison.Ordinal))
                     .Where(static project => !string.IsNullOrWhiteSpace(project.FilePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projectCompatibility = _workspaceLoader.InspectCompatibility(project.FilePath!);
            if (projectCompatibility.Diagnostics.Count > 0)
            {
                session.LoadedWorkspace.Dispose();
                return _resultFactory.Rejected<WorkspaceOpenOutcome>(
                    WorkspaceErrorCodes.WorkspaceLoadFailed,
                    "The workspace could not be loaded.",
                    diagnostics: projectCompatibility.Diagnostics);
            }

            if (!projectCompatibility.IsSdkStyle)
            {
                session.LoadedWorkspace.Dispose();
                return _resultFactory.Rejected<WorkspaceOpenOutcome>(
                    WorkspaceErrorCodes.WorkspaceNotSupported,
                    "Only SDK-style C# projects are supported.");
            }
        }

        var latestValidationError = _sessionStore.TryAddWorkspace(session, snapshot =>
        {
            return ValidateOpenCapacity(snapshot) ?? ValidateOpenUniqueness(snapshot, normalizedPath, normalizedAlias);
        });
        if (latestValidationError is not null)
        {
            session.LoadedWorkspace.Dispose();
            return _resultFactory.Rejected<WorkspaceOpenOutcome>(latestValidationError);
        }

        var workspaceInUse = await _instanceStatusPublisher.OpenAsync(
            session.Workspace.WorkspaceId,
            session.Workspace.WorkspaceRoot,
            session.Workspace.LoadedPath,
            session.State,
            cancellationToken).ConfigureAwait(false);
        session = session with
        {
            InputManifest = _workspaceChangeDetector.BuildManifest(session.CurrentSolution, session.Workspace.LoadedPath),
        };
        _sessionStore.ReplaceSession(session);
        var openDiagnostics = workspaceInUse
            ? session.LoadDiagnostics.Append(new DiagnosticInfo
            {
                Id = "WorkspaceInUse",
                Severity = Contracts.Results.DiagnosticSeverity.Warning,
                Message = "Another Roslyn Workbench MCP instance has this workspace open.",
            }).ToArray()
            : session.LoadDiagnostics;

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

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return _resultFactory.Rejected<WorkspaceCloseOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, CreateWorkspaceSelector(workspaceId, alias, path));
        if (selectionResult.HasError)
        {
            return _resultFactory.Rejected<WorkspaceCloseOutcome>(selectionResult.Error);
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return _resultFactory.Rejected<WorkspaceCloseOutcome>(
                WorkspaceErrorCodes.WorkspaceBusy,
                "The workspace is busy.",
                RequiredAction.Retry,
                CreateContext(selection.Session));
        }

        await using var leaseScope = lease;
        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        if (session is null)
        {
            return _resultFactory.Rejected<WorkspaceCloseOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        if (session.State is WorkspaceLifecycleState.TransactionActive or WorkspaceLifecycleState.TransactionConflicted)
        {
            return _resultFactory.Rejected<WorkspaceCloseOutcome>(
                "TransactionOpen",
                "Commit or roll back the active transaction before invoking this tool.",
                RequiredAction.CommitOrRollback,
                CreateContext(session));
        }

        var removedSession = _sessionStore.RemoveWorkspace(selection.WorkspaceId);
        if (removedSession is null)
        {
            return _resultFactory.Rejected<WorkspaceCloseOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        _instanceStatusPublisher.Close(removedSession.Workspace.WorkspaceId);
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

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return _resultFactory.Rejected<WorkspaceStatusOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, CreateWorkspaceSelector(workspaceId, alias, path));
        if (selectionResult.HasError)
        {
            return _resultFactory.Rejected<WorkspaceStatusOutcome>(selectionResult.Error);
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireShared();
        if (lease is null)
        {
            return _resultFactory.Rejected<WorkspaceStatusOutcome>(
                WorkspaceErrorCodes.WorkspaceBusy,
                "The workspace is busy.",
                RequiredAction.Retry,
                CreateContext(selection.Session));
        }

        await using var leaseScope = lease;
        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        if (session is null)
        {
            return _resultFactory.Rejected<WorkspaceStatusOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        if (session.State is WorkspaceLifecycleState.Ready or WorkspaceLifecycleState.TransactionActive
            && _workspaceChangeDetector.HasChanged(session.InputManifest, cancellationToken))
        {
            session = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
            _sessionStore.ReplaceSession(session);
        }

        var instances = await _instanceStatusPublisher.GetOtherLiveInstancesAsync(
            session.Workspace.WorkspaceRoot,
            cancellationToken).ConfigureAwait(false);
        return _resultFactory.Succeeded(CreateStatusOutcome(session, detail, instances), CreateContext(session));
    }

    public async ValueTask<WorkspaceOperationResult<WorkspaceReloadOutcome>> ReloadAsync(string? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return _resultFactory.Rejected<WorkspaceReloadOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, CreateWorkspaceSelector(workspaceId, alias, path));
        if (selectionResult.HasError)
        {
            return _resultFactory.Rejected<WorkspaceReloadOutcome>(selectionResult.Error);
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return _resultFactory.Rejected<WorkspaceReloadOutcome>(
                WorkspaceErrorCodes.WorkspaceBusy,
                "The workspace is busy.",
                RequiredAction.Retry,
                CreateContext(selection.Session));
        }

        await using var leaseScope = lease;
        var currentSession = _sessionStore.ReadSession(selection.WorkspaceId);
        if (currentSession is null)
        {
            return _resultFactory.Rejected<WorkspaceReloadOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

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

        if (string.Equals(Path.GetExtension(currentSession.Workspace.LoadedPath), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var preflight = _workspaceLoader.InspectCompatibility(currentSession.Workspace.LoadedPath);
            if (preflight.Diagnostics.Count > 0)
            {
                return _resultFactory.Rejected<WorkspaceReloadOutcome>(
                    WorkspaceErrorCodes.WorkspaceLoadFailed,
                    "The workspace could not be reloaded.",
                    context: context,
                    diagnostics: preflight.Diagnostics);
            }

            if (!preflight.IsSdkStyle)
            {
                return _resultFactory.Rejected<WorkspaceReloadOutcome>(
                    WorkspaceErrorCodes.WorkspaceNotSupported,
                    "Only SDK-style C# projects are supported.",
                    context: context);
            }
        }

        var loadedWorkspace = await _workspaceLoader.LoadAsync(currentSession.Workspace.LoadedPath, cancellationToken).ConfigureAwait(false);
        if (loadedWorkspace.Solution is null || loadedWorkspace.Workspace is null)
        {
            return _resultFactory.Rejected<WorkspaceReloadOutcome>(
                WorkspaceErrorCodes.WorkspaceLoadFailed,
                "The workspace could not be reloaded.",
                context: context,
                diagnostics: loadedWorkspace.Diagnostics);
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
            currentSession.OperationGate);

        var oldSession = _sessionStore.ReadSession(selection.WorkspaceId);
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

    private WorkspaceSessionSnapshot CreateSessionSnapshot(
        string workspaceId,
        string? alias,
        ILoadedWorkspace workspace,
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        long workspaceEpoch,
        IReadOnlyList<DiagnosticInfo> diagnostics,
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
            InputManifest = _workspaceChangeDetector.BuildManifest(solution, loadedPath),
            OperationGate = operationGate ?? new WorkspaceOperationGate(_options.MaxConcurrentQueries),
        };
    }

    private static WorkspaceStatusOutcome CreateStatusOutcome(
        WorkspaceSessionSnapshot session,
        StatusDetailLevel detail,
        IReadOnlyList<WorkspaceInstanceInfo> instances)
    {
        return new WorkspaceStatusOutcome
        {
            State = session.State,
            Workspace = session.Workspace,
            ProjectCount = session.ProjectCount,
            DocumentCount = session.DocumentCount,
            LoadDiagnostics = detail == StatusDetailLevel.Full ? session.LoadDiagnostics : null,
            Transaction = session.Transaction?.ToInfo(session.State == WorkspaceLifecycleState.TransactionConflicted),
            ReloadRequired = session.State == WorkspaceLifecycleState.WorkspaceOutOfDate,
            Instances = instances,
        };
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
}
