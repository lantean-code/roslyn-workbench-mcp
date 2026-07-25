using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Repositories;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.StateSequences;

internal sealed class StateSequenceRunner
{
    private readonly ScenarioHost _host;
    private readonly string _repositoryRoot;
    private readonly string _workspaceId;

    public StateSequenceRunner(
        ScenarioHost host,
        string workspaceId,
        string repositoryRoot)
    {
        _host = host;
        _workspaceId = workspaceId;
        _repositoryRoot = repositoryRoot;
    }

    public Task<StateSequenceExecution> ExecuteAsync(
        ScenarioDefinition scenario,
        CancellationToken cancellationToken)
    {
        var definition = scenario.StateSequence
            ?? throw new InvalidOperationException(
                $"Scenario '{scenario.Id}' does not define a state sequence.");

        return definition.Kind switch
        {
            StateSequenceKind.ExternalReload => ExecuteExternalReloadAsync(
                scenario,
                definition,
                cancellationToken),
            StateSequenceKind.LiveBuild => ExecuteLiveBuildAsync(
                scenario,
                definition,
                cancellationToken),
            StateSequenceKind.MultiRevisionCommit => ExecuteMultiRevisionCommitAsync(
                scenario,
                definition,
                cancellationToken),
            StateSequenceKind.WatcherStress => ExecuteWatcherStressAsync(
                scenario,
                definition,
                cancellationToken),
            _ => throw new InvalidDataException(
                $"Scenario '{scenario.Id}' has unknown state sequence kind '{definition.Kind}'."),
        };
    }

    private async Task<StateSequenceExecution> ExecuteExternalReloadAsync(
        ScenarioDefinition scenario,
        StateSequenceDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.Mutations.Count > 0)
        {
            throw new InvalidDataException(
                $"External-reload scenario '{scenario.Id}' must not define transaction mutations.");
        }

        var externalMutation = definition.ExternalMutation
            ?? throw new InvalidDataException(
                $"External-reload scenario '{scenario.Id}' does not define an external mutation.");
        var steps = new List<StateSequenceStepMeasurement>();
        var queryArguments = Materialize(scenario.Arguments);

        var baseline = await InvokeRequiredAsync(
            "baseline-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(baseline);
        var cached = await InvokeRequiredAsync(
            "cached-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(cached);
        ValidateEquivalentQueries(baseline, cached);

        await using var insertion = await ExternalMemberInsertion.ApplyAsync(
            _repositoryRoot,
            externalMutation,
            cancellationToken);

        var stale = await InvokeAsync(
            "stale-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(stale);
        if (!stale.IsError
            || !string.Equals(
                stale.ErrorCode,
                "WorkspaceOutOfDate",
                StringComparison.Ordinal)
            || !string.Equals(
                stale.RequiredAction,
                "ReloadWorkspace",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The externally changed workspace did not reject the stale query with WorkspaceOutOfDate and ReloadWorkspace.");
        }

        var reload = await InvokeRequiredAsync(
            "workspace-reload",
            "workspace-reload",
            CreateWorkspaceArguments(),
            cancellationToken);

        steps.Add(reload);
        var refreshed = await InvokeRequiredAsync(
            "refreshed-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(refreshed);
        ValidateExternalRefresh(baseline, refreshed);

        return new StateSequenceExecution { Steps = steps };
    }

    private async Task<StateSequenceExecution> ExecuteLiveBuildAsync(
        ScenarioDefinition scenario,
        StateSequenceDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.ExternalMutation is not null || definition.Mutations.Count > 0)
        {
            throw new InvalidDataException(
                $"Live-build scenario '{scenario.Id}' must not define external or transaction mutations.");
        }

        var build = definition.Build
            ?? throw new InvalidDataException(
                $"Live-build scenario '{scenario.Id}' does not define a build command.");

        var steps = new List<StateSequenceStepMeasurement>();
        var queryArguments = Materialize(scenario.Arguments);
        var baseline = await InvokeRequiredAsync(
            "baseline-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(baseline);
        var cached = await InvokeRequiredAsync(
            "cached-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(cached);
        ValidateEquivalentQueries(baseline, cached);

        var command = await RunBuildAsync(build, cancellationToken);

        var status = await InvokeRequiredAsync(
            "post-build-status",
            "workspace-status",
            CreateWorkspaceArguments(),
            cancellationToken);

        steps.Add(status);
        if (string.Equals(
            status.WorkspaceState,
            "Ready",
            StringComparison.Ordinal))
        {
            var postBuild = await InvokeRequiredAsync(
                "post-build-query",
                scenario.Tool,
                queryArguments,
                cancellationToken);

            steps.Add(postBuild);
            ValidateEquivalentQueries(baseline, postBuild);
            return new StateSequenceExecution
            {
                Steps = steps,
                ExternalCommand = command,
            };
        }

        if (!string.Equals(
            status.WorkspaceState,
            "WorkspaceOutOfDate",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The Workspace entered unexpected state '{status.WorkspaceState ?? "unknown"}' after the live build.");
        }

        var stale = await InvokeAsync(
            "post-build-stale-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(stale);
        ValidateStaleQuery(stale);
        var reload = await InvokeRequiredAsync(
            "post-build-reload",
            "workspace-reload",
            CreateWorkspaceArguments(),
            cancellationToken);

        steps.Add(reload);
        var refreshed = await InvokeRequiredAsync(
            "post-reload-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(refreshed);

        return new StateSequenceExecution
        {
            Steps = steps,
            ExternalCommand = command,
        };
    }

    private async Task<StateSequenceExecution> ExecuteWatcherStressAsync(
        ScenarioDefinition scenario,
        StateSequenceDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.Build is not null
            || definition.ExternalMutation is not null
            || definition.Mutations.Count > 0)
        {
            throw new InvalidDataException(
                $"Watcher-stress scenario '{scenario.Id}' must not define a build, external mutation or transaction mutations.");
        }

        var watcherStress = definition.WatcherStress
            ?? throw new InvalidDataException(
                $"Watcher-stress scenario '{scenario.Id}' does not define its generated artifact workload.");

        var steps = new List<StateSequenceStepMeasurement>();
        var queryArguments = Materialize(scenario.Arguments);
        var initialStatus = await InvokeRequiredAsync(
            "pre-stress-status",
            "workspace-status",
            CreateWorkspaceArguments(),
            cancellationToken);

        steps.Add(initialStatus);
        if (!string.Equals(
            initialStatus.WorkspaceState,
            "Ready",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The Workspace was not ready before watcher stress. State: '{initialStatus.WorkspaceState ?? "unknown"}'.");
        }

        var stressMeasurement = await RunWatcherStressAsync(
            watcherStress,
            cancellationToken);

        var status = await InvokeRequiredAsync(
            "post-stress-status",
            "workspace-status",
            CreateWorkspaceArguments(),
            cancellationToken);

        steps.Add(status);
        if (string.Equals(
            status.WorkspaceState,
            "Ready",
            StringComparison.Ordinal))
        {
            var postStress = await InvokeRequiredAsync(
                "post-stress-query",
                scenario.Tool,
                queryArguments,
                cancellationToken);

            steps.Add(postStress);
            return new StateSequenceExecution
            {
                Steps = steps,
                WatcherStress = stressMeasurement,
            };
        }

        ValidateWatcherStressOverflow(status);
        var stale = await InvokeAsync(
            "post-stress-stale-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(stale);
        ValidateStaleQuery(stale);
        var reload = await InvokeRequiredAsync(
            "post-stress-reload",
            "workspace-reload",
            CreateWorkspaceArguments(),
            cancellationToken);

        steps.Add(reload);
        var refreshed = await InvokeRequiredAsync(
            "post-reload-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(refreshed);

        return new StateSequenceExecution
        {
            Steps = steps,
            WatcherStress = stressMeasurement,
        };
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A failed multi-revision manual scenario must attempt transaction rollback before preserving and rethrowing the original failure.")]
    private async Task<StateSequenceExecution> ExecuteMultiRevisionCommitAsync(
        ScenarioDefinition scenario,
        StateSequenceDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.ExternalMutation is not null)
        {
            throw new InvalidDataException(
                $"Multi-revision scenario '{scenario.Id}' must not define an external mutation.");
        }

        if (definition.Mutations.Count != 2)
        {
            throw new InvalidDataException(
                $"Multi-revision scenario '{scenario.Id}' must define exactly two mutations.");
        }

        var steps = new List<StateSequenceStepMeasurement>();
        var queryArguments = Materialize(scenario.Arguments);
        var transactionStarted = false;
        try
        {
            var baseline = await InvokeRequiredAsync(
                "baseline-query",
                scenario.Tool,
                queryArguments,
                cancellationToken);

            steps.Add(baseline);
            var cached = await InvokeRequiredAsync(
                "cached-query",
                scenario.Tool,
                queryArguments,
                cancellationToken);

            steps.Add(cached);
            ValidateEquivalentQueries(baseline, cached);

            var start = await InvokeRequiredAsync(
                "transaction-start",
                "transaction-start",
                CreateWorkspaceArguments(),
                cancellationToken);

            steps.Add(start);
            transactionStarted = true;

            for (var index = 0; index < definition.Mutations.Count; index++)
            {
                var mutation = definition.Mutations[index];
                var step = await InvokeRequiredAsync(
                    $"mutation-{index + 1}",
                    mutation.Tool,
                    Materialize(mutation.Arguments),
                    cancellationToken);

                steps.Add(step);
                if (step.MutationStaged != true)
                {
                    throw new InvalidOperationException(
                        $"Mutation {index + 1} did not report a staged change.");
                }
            }

            var preview = await InvokeRequiredAsync(
                "transaction-preview",
                "transaction-preview",
                CreateWorkspaceArguments(),
                cancellationToken);

            steps.Add(preview);
            ValidateTransactionState(
                preview,
                expectedRevision: 2,
                expectedRevisionCount: 2);

            var undo = await InvokeRequiredAsync(
                "history-undo",
                "transaction-history",
                CreateHistoryArguments("Undo", transactionRevision: 2),
                cancellationToken);

            steps.Add(undo);
            ValidateTransactionState(
                undo,
                expectedRevision: 1,
                expectedRevisionCount: 2,
                expectedCanRedo: true);

            var redo = await InvokeRequiredAsync(
                "history-redo",
                "transaction-history",
                CreateHistoryArguments("Redo", transactionRevision: 1),
                cancellationToken);

            steps.Add(redo);
            ValidateTransactionState(
                redo,
                expectedRevision: 2,
                expectedRevisionCount: 2,
                expectedCanUndo: true);

            var commit = await InvokeRequiredAsync(
                "transaction-commit",
                "transaction-commit",
                CreateMutationArguments(transactionRevision: 2),
                cancellationToken);

            steps.Add(commit);
            transactionStarted = false;

            var refreshed = await InvokeRequiredAsync(
                "post-commit-query",
                scenario.Tool,
                queryArguments,
                cancellationToken);

            steps.Add(refreshed);
            ValidateCommittedRefresh(baseline, refreshed);

            return new StateSequenceExecution { Steps = steps };
        }
        catch
        {
            if (transactionStarted)
            {
                _ = await InvokeAsync(
                    "failure-rollback",
                    "transaction-rollback",
                    CreateWorkspaceArguments(),
                    CancellationToken.None);
            }

            throw;
        }
    }

    private async Task<StateSequenceStepMeasurement> InvokeRequiredAsync(
        string name,
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var step = await InvokeAsync(
            name,
            tool,
            arguments,
            cancellationToken);

        if (step.IsError)
        {
            throw new InvalidOperationException(
                $"State sequence step '{name}' returned '{step.ErrorCode ?? "an MCP error"}'.");
        }

        return step;
    }

    private async Task<StateSequenceStepMeasurement> InvokeAsync(
        string name,
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await _host.CallToolAsync(
            tool,
            arguments,
            cancellationToken);

        stopwatch.Stop();
        return CreateStep(name, tool, stopwatch.Elapsed.TotalMilliseconds, result);
    }

    private IReadOnlyDictionary<string, object?> Materialize(JsonElement arguments)
    {
        return ArgumentMaterializer.Materialize(
            arguments,
            _workspaceId,
            _repositoryRoot,
            _host.GetWorkspaceEpoch(_workspaceId));
    }

    private Dictionary<string, object?> CreateWorkspaceArguments()
    {
        var workspace = new Dictionary<string, object?>
        {
            ["workspaceId"] = _workspaceId,
        };

        return new Dictionary<string, object?>
        {
            ["workspace"] = workspace,
        };
    }

    private Dictionary<string, object?> CreateHistoryArguments(
        string direction,
        int transactionRevision)
    {
        var arguments = CreateMutationArguments(transactionRevision);
        arguments["direction"] = direction;
        return arguments;
    }

    private Dictionary<string, object?> CreateMutationArguments(int transactionRevision)
    {
        var arguments = CreateWorkspaceArguments();
        arguments["expectedSnapshot"] = new Dictionary<string, object?>
        {
            ["workspaceId"] = _workspaceId,
            ["workspaceEpoch"] = _host.GetWorkspaceEpoch(_workspaceId),
            ["transactionRevision"] = transactionRevision,
        };

        return arguments;
    }

    private async Task<ExternalCommandMeasurement> RunBuildAsync(
        CommandDefinition build,
        CancellationToken cancellationToken)
    {
        var fileName = OperatingSystem.IsWindows()
            && !string.IsNullOrWhiteSpace(build.WindowsFileName)
                ? build.WindowsFileName
                : build.FileName;

        var arguments = OperatingSystem.IsWindows()
            && build.WindowsArguments is not null
                ? build.WindowsArguments
                : build.Arguments;

        var environment = new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = RepositoryManager.GetNuGetPackagesDirectory(
                _repositoryRoot),
        };

        await Console.Out.WriteLineAsync(
            $"> {fileName} {string.Join(' ', arguments)}");

        var before = _host.CaptureSnapshot();
        var stopwatch = Stopwatch.StartNew();
        var result = await ExternalCommand.RunAsync(
            fileName,
            arguments,
            _repositoryRoot,
            cancellationToken,
            environment);

        stopwatch.Stop();
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        var after = _host.CaptureSnapshot();
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Live build '{fileName}' failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
        }

        return new ExternalCommandMeasurement
        {
            FileName = fileName,
            Arguments = arguments,
            ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            ExitCode = result.ExitCode,
            HostCpuMilliseconds = (after.CpuTime - before.CpuTime).TotalMilliseconds,
            HostWorkingSetBeforeBytes = before.WorkingSetBytes,
            HostWorkingSetAfterBytes = after.WorkingSetBytes,
            HostWorkingSetDeltaBytes = after.WorkingSetBytes - before.WorkingSetBytes,
            HostPeakWorkingSetBytes = after.PeakWorkingSetBytes,
            StandardOutputBytes = Encoding.UTF8.GetByteCount(
                result.StandardOutput),
            StandardErrorBytes = Encoding.UTF8.GetByteCount(
                result.StandardError),
        };
    }

    private async Task<WatcherStressMeasurement> RunWatcherStressAsync(
        WatcherStressDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.FileCount <= 0 || definition.WritePasses < 0)
        {
            throw new InvalidDataException(
                "Watcher stress requires a positive file count and a non-negative write-pass count.");
        }

        var artifactRoot = ResolveStressArtifactRoot(definition.ArtifactPath);
        var artifactRootExisted = Directory.Exists(artifactRoot);
        var stressRoot = Path.Combine(
            artifactRoot,
            $".roslyn-workbench-watcher-stress-{Guid.NewGuid():N}");

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount,
        };

        var before = _host.CaptureSnapshot();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Directory.CreateDirectory(stressRoot);
            Parallel.For(
                0,
                definition.FileCount,
                parallelOptions,
                index => File.WriteAllText(
                    Path.Combine(stressRoot, $"{index:D8}.tmp"),
                    "0"));

            for (var pass = 0; pass < definition.WritePasses; pass++)
            {
                var content = pass.ToString(CultureInfo.InvariantCulture);
                Parallel.For(
                    0,
                    definition.FileCount,
                    parallelOptions,
                    index => File.WriteAllText(
                        Path.Combine(stressRoot, $"{index:D8}.tmp"),
                        content));
            }

            Parallel.For(
                0,
                definition.FileCount,
                parallelOptions,
                index => File.Delete(
                    Path.Combine(stressRoot, $"{index:D8}.tmp")));
        }
        finally
        {
            if (Directory.Exists(stressRoot))
            {
                Directory.Delete(stressRoot, recursive: true);
            }

            if (!artifactRootExisted
                && Directory.Exists(artifactRoot)
                && !Directory.EnumerateFileSystemEntries(artifactRoot).Any())
            {
                Directory.Delete(artifactRoot);
            }
        }

        stopwatch.Stop();
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        var after = _host.CaptureSnapshot();

        return new WatcherStressMeasurement
        {
            ArtifactPath = definition.ArtifactPath,
            FileCount = definition.FileCount,
            WritePasses = definition.WritePasses,
            ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            HostCpuMilliseconds = (after.CpuTime - before.CpuTime).TotalMilliseconds,
            HostWorkingSetBeforeBytes = before.WorkingSetBytes,
            HostWorkingSetAfterBytes = after.WorkingSetBytes,
            HostWorkingSetDeltaBytes = after.WorkingSetBytes - before.WorkingSetBytes,
            HostPeakWorkingSetBytes = after.PeakWorkingSetBytes,
        };
    }

    private string ResolveStressArtifactRoot(string artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            throw new InvalidDataException(
                "Watcher stress requires a repository-relative artifact path.");
        }

        var artifactRoot = Path.GetFullPath(
            Path.Combine(_repositoryRoot, artifactPath));

        var relativePath = Path.GetRelativePath(_repositoryRoot, artifactRoot);
        if (string.Equals(relativePath, ".", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath)
            || string.Equals(relativePath, "..", StringComparison.Ordinal)
            || relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Watcher-stress artifact path '{artifactPath}' must identify a child of the repository root.");
        }

        return artifactRoot;
    }

    private static StateSequenceStepMeasurement CreateStep(
        string name,
        string tool,
        double elapsedMilliseconds,
        CallToolResult result)
    {
        var observation = ResponseObservation.Create(result);
        var content = result.StructuredContent;
        var errorCode = TryGetString(content, "error", "code");
        var requiredAction = TryGetString(content, "next");
        var transaction = TryGetObject(content, "data", "transaction");
        if (transaction is null)
        {
            transaction = TryGetObject(content, "data", "mutation", "transaction");
        }

        var references = TryGetObject(content, "data", "references");
        var externalChange = TryGetObject(content, "data", "externalChange");

        return new StateSequenceStepMeasurement
        {
            Name = name,
            Tool = tool,
            ElapsedMilliseconds = elapsedMilliseconds,
            IsError = result.IsError == true,
            ResponseSha256 = observation.Sha256,
            ErrorCode = errorCode,
            RequiredAction = requiredAction,
            WorkspaceState = TryGetString(content, "data", "state"),
            ExternalChange = CreateExternalChange(externalChange),
            MutationStaged = observation.MutationStaged,
            ReferenceCount = GetReferenceCount(references),
            DefinitionPaths = GetDefinitionPaths(references),
            TransactionRevision = TryGetInt32(transaction, "revision"),
            TransactionRevisionCount = TryGetInt32(transaction, "revisionCount"),
            CanUndo = TryGetBoolean(transaction, "canUndo"),
            CanRedo = TryGetBoolean(transaction, "canRedo"),
        };
    }

    private static WorkspaceExternalChangeMeasurement? CreateExternalChange(
        JsonElement? externalChange)
    {
        var detectionSource = TryGetString(externalChange, "detectionSource");
        var kind = TryGetString(externalChange, "kind");
        if (detectionSource is null || kind is null)
        {
            return null;
        }

        return new WorkspaceExternalChangeMeasurement
        {
            DetectionSource = detectionSource,
            ErrorCode = TryGetString(externalChange, "errorCode"),
            Kind = kind,
            Path = TryGetString(externalChange, "path"),
            PreviousPath = TryGetString(externalChange, "previousPath"),
        };
    }

    private static void ValidateEquivalentQueries(
        StateSequenceStepMeasurement baseline,
        StateSequenceStepMeasurement cached)
    {
        if (!string.Equals(
            baseline.ResponseSha256,
            cached.ResponseSha256,
            StringComparison.Ordinal)
            || baseline.ReferenceCount != cached.ReferenceCount)
        {
            throw new InvalidOperationException(
                "The warmed query did not reproduce the baseline response.");
        }
    }

    private static void ValidateStaleQuery(StateSequenceStepMeasurement stale)
    {
        if (!stale.IsError
            || !string.Equals(
                stale.ErrorCode,
                "WorkspaceOutOfDate",
                StringComparison.Ordinal)
            || !string.Equals(
                stale.RequiredAction,
                "ReloadWorkspace",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The post-build stale query did not require a Workspace reload.");
        }
    }

    private static void ValidateWatcherStressOverflow(
        StateSequenceStepMeasurement status)
    {
        var externalChange = status.ExternalChange;
        if (!string.Equals(
            status.WorkspaceState,
            "WorkspaceOutOfDate",
            StringComparison.Ordinal)
            || externalChange is null
            || !string.Equals(
                externalChange.Kind,
                "WatcherError",
                StringComparison.Ordinal)
            || !string.Equals(
                externalChange.ErrorCode,
                "WatcherBufferOverflow",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Watcher stress entered unexpected state '{status.WorkspaceState ?? "unknown"}' with error '{externalChange?.ErrorCode ?? "none"}'.");
        }
    }

    private static void ValidateExternalRefresh(
        StateSequenceStepMeasurement baseline,
        StateSequenceStepMeasurement refreshed)
    {
        if (baseline.ReferenceCount is null
            || refreshed.ReferenceCount <= baseline.ReferenceCount
            || string.Equals(
                baseline.ResponseSha256,
                refreshed.ResponseSha256,
                StringComparison.Ordinal))
        {
            var baselineCount = baseline.ReferenceCount?.ToString(
                CultureInfo.InvariantCulture) ?? "unknown";

            var refreshedCount = refreshed.ReferenceCount?.ToString(
                CultureInfo.InvariantCulture) ?? "unknown";

            var responseChanged = !string.Equals(
                baseline.ResponseSha256,
                refreshed.ResponseSha256,
                StringComparison.Ordinal);

            throw new InvalidOperationException(
                $"The reloaded query did not contain externally added semantic references. Baseline count: {baselineCount}; refreshed count: {refreshedCount}; response changed: {responseChanged}.");
        }
    }

    private static void ValidateCommittedRefresh(
        StateSequenceStepMeasurement baseline,
        StateSequenceStepMeasurement refreshed)
    {
        var baselineInOriginalFile = ContainsFileName(
            baseline.DefinitionPaths,
            "Guard.cs");
        var refreshedInCreatedFile = ContainsFileName(
            refreshed.DefinitionPaths,
            "NoEnumerationAttribute.cs");

        if (!baselineInOriginalFile
            || !refreshedInCreatedFile
            || string.Equals(
                baseline.ResponseSha256,
                refreshed.ResponseSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The post-commit query did not resolve the moved definition from the committed solution.");
        }
    }

    private static void ValidateTransactionState(
        StateSequenceStepMeasurement step,
        int expectedRevision,
        int expectedRevisionCount,
        bool? expectedCanUndo = null,
        bool? expectedCanRedo = null)
    {
        if (step.TransactionRevision != expectedRevision
            || step.TransactionRevisionCount != expectedRevisionCount
            || (expectedCanUndo is not null
                && step.CanUndo != expectedCanUndo)
            || (expectedCanRedo is not null
                && step.CanRedo != expectedCanRedo))
        {
            throw new InvalidOperationException(
                $"Step '{step.Name}' did not report the expected transaction history state.");
        }
    }

    private static bool ContainsFileName(
        IReadOnlyList<string> paths,
        string fileName)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return paths.Any(path => string.Equals(
            Path.GetFileName(path),
            fileName,
            comparison));
    }

    private static JsonElement? TryGetObject(
        JsonElement? content,
        params string[] path)
    {
        if (content is not JsonElement value)
        {
            return null;
        }

        foreach (var property in path)
        {
            if (value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty(property, out value))
            {
                return null;
            }
        }

        return value.ValueKind == JsonValueKind.Object
            ? value
            : null;
    }

    private static string? TryGetString(
        JsonElement? content,
        params string[] path)
    {
        if (content is not JsonElement value)
        {
            return null;
        }

        foreach (var property in path)
        {
            if (value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty(property, out value))
            {
                return null;
            }
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? TryGetInt32(
        JsonElement? value,
        string property)
    {
        if (value is not JsonElement element
            || !element.TryGetProperty(property, out var result)
            || !result.TryGetInt32(out var number))
        {
            return null;
        }

        return number;
    }

    private static bool? TryGetBoolean(
        JsonElement? value,
        string property)
    {
        if (value is not JsonElement element
            || !element.TryGetProperty(property, out var result)
            || result.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        return result.GetBoolean();
    }

    private static int? GetReferenceCount(JsonElement? references)
    {
        if (references is not JsonElement element
            || !element.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return items.GetArrayLength();
    }

    private static List<string> GetDefinitionPaths(
        JsonElement? references)
    {
        if (references is not JsonElement element
            || !element.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var paths = new List<string>();
        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("isDefinition", out var isDefinition)
                || isDefinition.ValueKind != JsonValueKind.True
                || !item.TryGetProperty("location", out var location)
                || !location.TryGetProperty("document", out var document)
                || !document.TryGetProperty("path", out var path)
                || path.GetString() is not { } value)
            {
                continue;
            }

            paths.Add(value);
        }

        return paths;
    }
}
