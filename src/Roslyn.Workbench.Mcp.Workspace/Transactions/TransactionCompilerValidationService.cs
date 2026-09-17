using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Performs lazy, snapshot-scoped compiler-error comparison for transaction baselines and staged solutions.
/// </summary>
internal sealed class TransactionCompilerValidationService : ITransactionCompilerValidationService
{
    private const string _cacheComponentIdentity = "transaction-compiler-errors";

    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceQueryCacheScopeFactory _cacheScopeFactory;
    private readonly IWorkspaceResolverFactory _resolverFactory;
    private readonly IWorkspacePathServiceFactory _pathServiceFactory;
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly WorkspaceOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCompilerValidationService"/> class.
    /// </summary>
    /// <param name="workspaceChangeDetector">The detector that prevents validation against stale external inputs.</param>
    /// <param name="cacheScopeFactory">The factory providing solution-snapshot-scoped diagnostic caching.</param>
    /// <param name="resolverFactory">The factory used to project introduced source diagnostics.</param>
    /// <param name="pathServiceFactory">The factory used to project Workspace-relative project paths.</param>
    /// <param name="pathComparison">The platform-aware path comparison used by diagnostic identity.</param>
    /// <param name="options">The Workspace options controlling result bounds.</param>
    public TransactionCompilerValidationService(
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceQueryCacheScopeFactory cacheScopeFactory,
        IWorkspaceResolverFactory resolverFactory,
        IWorkspacePathServiceFactory pathServiceFactory,
        IWorkspacePathComparison pathComparison,
        IOptions<WorkspaceOptions> options)
    {
        _workspaceChangeDetector = workspaceChangeDetector;
        _cacheScopeFactory = cacheScopeFactory;
        _resolverFactory = resolverFactory;
        _pathServiceFactory = pathServiceFactory;
        _pathComparison = pathComparison;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async ValueTask<TransactionCompilerValidationOutcome> ValidateAsync(
        WorkspaceSessionSnapshot session,
        CancellationToken cancellationToken)
    {
        var transaction = session.Transaction
            ?? throw new InvalidOperationException("Compiler validation requires an active transaction.");

        var stopwatch = Stopwatch.StartNew();
        var limitations = CreateLoadLimitations(session.LoadDiagnostics);
        var incompleteReasons = new HashSet<TransactionCompilerValidationIncompleteReason>();
        var externalInputsChanged = _workspaceChangeDetector.HasChanged(
            session.InputManifest,
            cancellationToken);

        if (externalInputsChanged)
        {
            incompleteReasons.Add(TransactionCompilerValidationIncompleteReason.ExternalWorkspaceInputsChanged);
            limitations.Add("External Workspace inputs changed after the transaction baseline was captured; reload and start a new transaction before relying on compiler validation.");
        }

        var hasLoadErrors = session.LoadDiagnostics.Any(
            static diagnostic => diagnostic.Severity == Results.DiagnosticSeverity.Error);

        if (hasLoadErrors)
        {
            incompleteReasons.Add(TransactionCompilerValidationIncompleteReason.WorkspaceLoadError);
        }

        if (externalInputsChanged || hasLoadErrors)
        {
            stopwatch.Stop();
            return CreateIncompleteOutcome(
                session,
                transaction,
                incompleteReasons,
                limitations,
                stopwatch.ElapsedMilliseconds);
        }

        var affectedProjectIds = GetAffectedProjectIds(
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            session.ProjectTargetFrameworks);

        var projectValidations = new List<TransactionCompilerProjectValidation>(affectedProjectIds.Count);
        var introducedDiagnostics = new List<CachedCompilerDiagnostic>();
        var baselineErrorCount = 0;
        var stagedErrorCount = 0;
        var introducedErrorCount = 0;
        var complete = true;

        foreach (var projectId in affectedProjectIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var baselineProject = transaction.BaselineSolution.GetProject(projectId);
            var stagedProject = transaction.CurrentSolution.GetProject(projectId);
            var projectName = GetProjectName(
                session,
                stagedProject,
                baselineProject,
                projectId);

            var targetFramework = session.ProjectTargetFrameworks.GetTargetFramework(projectId);

            if (baselineProject is null || stagedProject is null)
            {
                complete = false;
                incompleteReasons.Add(TransactionCompilerValidationIncompleteReason.ProjectUnavailable);
                limitations.Add($"Project '{projectName}' was not available in both transaction snapshots.");
                projectValidations.Add(CreateIncompleteProject(projectName, targetFramework));
                continue;
            }

            var baselineDiagnostics = await GetCompilerDiagnosticsAsync(
                session.Workspace.WorkspaceId,
                baselineProject,
                projectName,
                targetFramework,
                cancellationToken);

            var stagedDiagnostics = await GetCompilerDiagnosticsAsync(
                session.Workspace.WorkspaceId,
                stagedProject,
                projectName,
                targetFramework,
                cancellationToken);

            if (!baselineDiagnostics.IsComplete || !stagedDiagnostics.IsComplete)
            {
                complete = false;
                incompleteReasons.Add(TransactionCompilerValidationIncompleteReason.CompilationUnavailable);
                AddCompilationLimitation(limitations, projectName, baselineDiagnostics, stagedDiagnostics);
                projectValidations.Add(CreateIncompleteProject(projectName, targetFramework));
                continue;
            }

            var introduced = SubtractDiagnostics(
                baselineDiagnostics.Diagnostics,
                stagedDiagnostics.Diagnostics);

            baselineErrorCount += baselineDiagnostics.Diagnostics.Count;
            stagedErrorCount += stagedDiagnostics.Diagnostics.Count;
            introducedErrorCount += introduced.Count;
            introducedDiagnostics.AddRange(introduced);
            projectValidations.Add(new TransactionCompilerProjectValidation
            {
                Project = projectName,
                TargetFramework = targetFramework,
                IsComplete = true,
                BaselineErrorCount = baselineDiagnostics.Diagnostics.Count,
                StagedErrorCount = stagedDiagnostics.Diagnostics.Count,
                IntroducedErrorCount = introduced.Count,
            });
        }

        var snapshot = WorkspaceSnapshotPreconditionFactory.Create(
            session.CurrentSnapshotIdentity,
            transaction.CurrentRevision);

        var resolver = _resolverFactory.Create(
            transaction.CurrentSolution,
            session.Workspace,
            session.ProjectTargetFrameworks,
            snapshot);

        var projectedDiagnostics = await ProjectDiagnosticsAsync(
            transaction.CurrentSolution,
            resolver,
            introducedDiagnostics,
            cancellationToken);

        stopwatch.Stop();

        return new TransactionCompilerValidationOutcome
        {
            IsComplete = complete,
            Succeeded = complete && introducedErrorCount == 0,
            IncompleteReasons = OrderIncompleteReasons(incompleteReasons),
            Transaction = transaction.ToInfo(session.State == WorkspaceLifecycleState.TransactionConflicted),
            BaselineErrorCount = baselineErrorCount,
            StagedErrorCount = stagedErrorCount,
            IntroducedErrorCount = introducedErrorCount,
            DurationMilliseconds = stopwatch.ElapsedMilliseconds,
            Projects = projectValidations,
            IntroducedDiagnostics = projectedDiagnostics,
            Limitations = limitations.Take(_options.DefaultMaxResults).ToArray(),
        };
    }

    private TransactionCompilerValidationOutcome CreateIncompleteOutcome(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        IReadOnlySet<TransactionCompilerValidationIncompleteReason> incompleteReasons,
        IReadOnlyList<string> limitations,
        long durationMilliseconds)
    {
        return new TransactionCompilerValidationOutcome
        {
            IsComplete = false,
            Succeeded = false,
            IncompleteReasons = OrderIncompleteReasons(incompleteReasons),
            Transaction = transaction.ToInfo(session.State == WorkspaceLifecycleState.TransactionConflicted),
            BaselineErrorCount = 0,
            StagedErrorCount = 0,
            IntroducedErrorCount = 0,
            DurationMilliseconds = durationMilliseconds,
            Limitations = limitations.Take(_options.DefaultMaxResults).ToArray(),
        };
    }

    private static TransactionCompilerValidationIncompleteReason[] OrderIncompleteReasons(
        IEnumerable<TransactionCompilerValidationIncompleteReason> incompleteReasons)
    {
        return incompleteReasons
            .Order()
            .ToArray();
    }

    private async ValueTask<ProjectCompilerDiagnostics> GetCompilerDiagnosticsAsync(
        Guid workspaceId,
        Project project,
        string projectName,
        string? targetFramework,
        CancellationToken cancellationToken)
    {
        var cacheScope = _cacheScopeFactory.CreateScope(
            workspaceId,
            project.Solution,
            _cacheComponentIdentity);

        var cacheKey = new ProjectCompilerDiagnosticsCacheKey(project.Id.Id);
        var result = await cacheScope.GetOrCreateAsync(
            cacheKey,
            async factoryCancellationToken => await CompileProjectAsync(
                project,
                projectName,
                targetFramework,
                factoryCancellationToken),
            static value => Math.Max(1, value.Diagnostics.Count),
            static _ => true,
            cancellationToken);

        if (result is null)
        {
            return ProjectCompilerDiagnostics.Incomplete("Compiler diagnostic caching did not produce a result.");
        }

        return result;
    }

    private async ValueTask<ProjectCompilerDiagnostics> CompileProjectAsync(
        Project project,
        string projectName,
        string? targetFramework,
        CancellationToken cancellationToken)
    {
        try
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                return ProjectCompilerDiagnostics.Incomplete("Roslyn did not provide a compilation.");
            }

            var diagnostics = compilation
                .GetDiagnostics(cancellationToken)
                .Where(IsCompilerError)
                .Select(diagnostic => CreateCachedDiagnostic(
                    project,
                    projectName,
                    targetFramework,
                    diagnostic))
                .ToArray();

            return ProjectCompilerDiagnostics.Complete(diagnostics);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            return ProjectCompilerDiagnostics.Incomplete(exception.Message);
        }
    }

    private CachedCompilerDiagnostic CreateCachedDiagnostic(
        Project project,
        string projectName,
        string? targetFramework,
        Diagnostic diagnostic)
    {
        var location = diagnostic.Location;
        var document = location.SourceTree is null
            ? null
            : project.Solution.GetDocument(location.SourceTree);

        var locationIdentity = GetLocationIdentity(location);
        var identity = new CompilerDiagnosticIdentity
        {
            Project = projectName,
            TargetFramework = targetFramework,
            Id = diagnostic.Id,
            Message = diagnostic.GetMessage(CultureInfo.InvariantCulture),
            Location = locationIdentity,
        };

        return new CachedCompilerDiagnostic
        {
            Identity = identity,
            DocumentId = document?.Id,
            SourceSpan = location.IsInSource ? location.SourceSpan : null,
        };
    }

    private FileSystemPathKey GetLocationIdentity(Location location)
    {
        if (location.IsInSource && !string.IsNullOrWhiteSpace(location.SourceTree?.FilePath))
        {
            return _pathComparison.CreateKey(location.SourceTree.FilePath);
        }

        return new FileSystemPathKey($"<{location.Kind}>", isCaseSensitive: true);
    }

    private static bool IsCompilerError(Diagnostic diagnostic)
    {
        return diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
            && !diagnostic.IsSuppressed
            && diagnostic.Descriptor.CustomTags.Contains(WellKnownDiagnosticTags.Compiler, StringComparer.Ordinal);
    }

    private async ValueTask<IReadOnlyList<DiagnosticInfo>> ProjectDiagnosticsAsync(
        Solution solution,
        IWorkspaceResolver resolver,
        List<CachedCompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var projected = new List<DiagnosticInfo>(Math.Min(diagnostics.Count, _options.DefaultMaxResults));
        foreach (var diagnostic in diagnostics.Take(_options.DefaultMaxResults))
        {
            ResolvedLocation? location = null;
            if (diagnostic.DocumentId is not null && diagnostic.SourceSpan is TextSpan sourceSpan)
            {
                var document = solution.GetDocument(diagnostic.DocumentId);
                SyntaxTree? syntaxTree = null;
                if (document is not null)
                {
                    syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
                }

                if (syntaxTree is not null)
                {
                    location = resolver.CreateResolvedLocation(syntaxTree.GetLocation(sourceSpan));
                }
            }

            projected.Add(new DiagnosticInfo
            {
                Id = diagnostic.Identity.Id,
                Severity = Results.DiagnosticSeverity.Error,
                Message = diagnostic.Identity.Message,
                Location = location,
            });
        }

        return projected;
    }

    private string GetProjectName(
        WorkspaceSessionSnapshot session,
        Project? stagedProject,
        Project? baselineProject,
        ProjectId projectId)
    {
        var project = stagedProject ?? baselineProject;
        if (project is null)
        {
            return projectId.Id.ToString();
        }

        var pathService = _pathServiceFactory.Create(session.Workspace);
        if (pathService.TryNormalizePath(project.FilePath ?? string.Empty, out var normalizedPath))
        {
            return normalizedPath;
        }

        return project.Name;
    }

    private static List<ProjectId> GetAffectedProjectIds(
        Solution baseline,
        Solution staged,
        WorkspaceProjectTargetFrameworkMap targetFrameworks)
    {
        var solutionChanges = staged.GetChanges(baseline);
        var changedProjectIds = solutionChanges
            .GetProjectChanges()
            .Select(static change => change.ProjectId)
            .ToHashSet();

        changedProjectIds.UnionWith(
            solutionChanges.GetAddedProjects().Select(static project => project.Id));

        changedProjectIds.UnionWith(
            solutionChanges.GetRemovedProjects().Select(static project => project.Id));

        var dependencyGraph = staged.GetProjectDependencyGraph();
        var affectedProjectIds = new HashSet<ProjectId>(changedProjectIds);
        foreach (var changedProjectId in changedProjectIds)
        {
            if (staged.GetProject(changedProjectId) is null)
            {
                continue;
            }

            affectedProjectIds.UnionWith(
                dependencyGraph.GetProjectsThatTransitivelyDependOnThisProject(changedProjectId));
        }

        return affectedProjectIds
            .OrderBy(projectId => staged.GetProject(projectId)?.FilePath, StringComparer.Ordinal)
            .ThenBy(projectId => staged.GetProject(projectId)?.Name, StringComparer.Ordinal)
            .ThenBy(targetFrameworks.GetTargetFramework, StringComparer.Ordinal)
            .ToList();
    }

    private static List<CachedCompilerDiagnostic> SubtractDiagnostics(
        IReadOnlyList<CachedCompilerDiagnostic> baseline,
        IReadOnlyList<CachedCompilerDiagnostic> staged)
    {
        var remainingBaseline = baseline
            .GroupBy(static diagnostic => diagnostic.Identity)
            .ToDictionary(static group => group.Key, static group => group.Count());

        var introduced = new List<CachedCompilerDiagnostic>();
        foreach (var diagnostic in staged)
        {
            if (remainingBaseline.TryGetValue(diagnostic.Identity, out var count) && count > 0)
            {
                remainingBaseline[diagnostic.Identity] = count - 1;
                continue;
            }

            introduced.Add(diagnostic);
        }

        return introduced;
    }

    private static List<string> CreateLoadLimitations(IReadOnlyList<DiagnosticInfo> diagnostics)
    {
        return diagnostics
            .Select(static diagnostic => $"Workspace load diagnostic {diagnostic.Id}: {diagnostic.Message}")
            .ToList();
    }

    private static void AddCompilationLimitation(
        List<string> limitations,
        string projectName,
        ProjectCompilerDiagnostics baseline,
        ProjectCompilerDiagnostics staged)
    {
        if (!baseline.IsComplete)
        {
            limitations.Add($"Baseline compilation for '{projectName}' was incomplete: {baseline.ErrorMessage}");
        }

        if (!staged.IsComplete)
        {
            limitations.Add($"Staged compilation for '{projectName}' was incomplete: {staged.ErrorMessage}");
        }
    }

    private static TransactionCompilerProjectValidation CreateIncompleteProject(
        string projectName,
        string? targetFramework)
    {
        return new TransactionCompilerProjectValidation
        {
            Project = projectName,
            TargetFramework = targetFramework,
            IsComplete = false,
            BaselineErrorCount = 0,
            StagedErrorCount = 0,
            IntroducedErrorCount = 0,
        };
    }

}
