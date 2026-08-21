using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Diagnostics;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Reporting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Repositories;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Cancellation;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CommitCancellation;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Concurrency;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Conflict;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CrashRecovery;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.StateSequences;
using Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Application;

internal static class ScenarioApplication
{
    private const string _allScenarios = "all";
    private const int _workspaceOpenMaximumAttempts = 3;
    private static readonly TimeSpan _workspaceOpenRetryDelay = TimeSpan.FromMilliseconds(250);

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The manual runner must preserve workload, workspace-close, and Host-disposal failures so cleanup is attempted and every failure is reported together.")]
    public static async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string? executionDirectory = null;
        try
        {
            var options = ScenarioOptions.Parse(arguments);
            if (options.Command == ScenarioCommand.Help)
            {
                WriteHelp();
                return 0;
            }

            var suite = await ScenarioSuiteLoader.LoadAsync(cancellationToken);
            if (options.Command == ScenarioCommand.List)
            {
                ListSuite(suite);
                return 0;
            }

            var repository = ResolveRepository(suite, options.Repository);
            var frameworkRoot = ResolveFrameworkRoot(options.FrameworkRoot);
            var cacheDirectory = ResolveCacheDirectory(options.CacheDirectory);
            using var cacheLock = ScenarioCacheLock.Acquire(cacheDirectory);
            var manager = new RepositoryManager(cacheDirectory);
            var repositoryRoot = await manager.PrepareAsync(
                repository,
                !options.SkipPreparation,
                cancellationToken);

            if (options.Command == ScenarioCommand.Prepare)
            {
                Console.WriteLine($"Prepared {repository.Id} at {repositoryRoot}");
                return 0;
            }

            var hostPath = ResolveRequiredPath(options.HostPath, "--host");
            var outputDirectory = ResolveOutputDirectory(options.OutputDirectory, frameworkRoot, repository.Id);
            executionDirectory = CreateExecutionDirectory(repository.Id);
            var scenarios = ResolveScenarios(repository, options.Scenario, options.Command);
            var environment = RunEnvironmentInfo.Capture(hostPath);

            if (options.Command == ScenarioCommand.Commit)
            {
                await MeasureDurableCommitsAsync(
                    options,
                    repository,
                    scenarios,
                    hostPath,
                    repositoryRoot,
                    environment,
                    executionDirectory,
                    outputDirectory,
                    cancellationToken);

                await Console.Out.WriteLineAsync($"Results: {outputDirectory}");
                return 0;
            }

            if (options.Command == ScenarioCommand.CommitCancellation)
            {
                await MeasureCommitCancellationsAsync(
                    options,
                    repository,
                    scenarios,
                    hostPath,
                    repositoryRoot,
                    environment,
                    executionDirectory,
                    outputDirectory,
                    cancellationToken);

                await Console.Out.WriteLineAsync($"Results: {outputDirectory}");
                return 0;
            }

            if (options.Command == ScenarioCommand.Conflict)
            {
                await MeasureConflictsAsync(
                    options,
                    repository,
                    scenarios,
                    hostPath,
                    repositoryRoot,
                    environment,
                    executionDirectory,
                    outputDirectory,
                    cancellationToken);

                await Console.Out.WriteLineAsync($"Results: {outputDirectory}");
                return 0;
            }

            if (options.Command == ScenarioCommand.CrashRecovery)
            {
                await MeasureCrashRecoveriesAsync(
                    options,
                    repository,
                    scenarios,
                    hostPath,
                    repositoryRoot,
                    environment,
                    executionDirectory,
                    outputDirectory,
                    cancellationToken);

                await Console.Out.WriteLineAsync($"Results: {outputDirectory}");
                return 0;
            }

            if (options.Command == ScenarioCommand.StateSequence)
            {
                await MeasureStateSequencesAsync(
                    options,
                    repository,
                    scenarios,
                    hostPath,
                    repositoryRoot,
                    environment,
                    executionDirectory,
                    outputDirectory,
                    cancellationToken);

                await Console.Out.WriteLineAsync($"Results: {outputDirectory}");
                return 0;
            }

            var stateDirectory = Path.Combine(executionDirectory, "state");
            var workspacePath = Path.Combine(repositoryRoot, repository.WorkspacePath);
            var initialWorkspaceStateFiles = RunStateValidator.CaptureWorkspaceStateFiles(repositoryRoot);

            await using var host = await ScenarioHost.StartAsync(
                hostPath,
                repositoryRoot,
                stateDirectory,
                options.PluginDirectory,
                cancellationToken);
            Guid? workspaceId = null;
            ExceptionDispatchInfo? runFailure = null;
            var workspaceClosed = false;

            try
            {
                var openedWorkspaceId = await OpenWorkspaceAsync(host, workspacePath, repositoryRoot, cancellationToken);
                workspaceId = openedWorkspaceId;
                var runner = new ToolInvocationRunner(host, openedWorkspaceId, repositoryRoot);

                if (options.Command == ScenarioCommand.Measure)
                {
                    await MeasureAsync(options, repository, scenarios, runner, environment, outputDirectory, cancellationToken);
                }
                else if (options.Command == ScenarioCommand.Cancel)
                {
                    await MeasureCancellationAsync(options, repository, scenarios, runner, environment, outputDirectory, cancellationToken);
                }
                else if (options.Command == ScenarioCommand.Concurrency)
                {
                    await MeasureConcurrencyAsync(
                        options,
                        repository,
                        scenarios,
                        host,
                        openedWorkspaceId,
                        repositoryRoot,
                        environment,
                        outputDirectory,
                        cancellationToken);
                }
                else
                {
                    await ProfileAsync(options, repository, scenarios, host, runner, environment, frameworkRoot, outputDirectory, cancellationToken);
                }
            }
            catch (Exception exception)
            {
                runFailure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                if (workspaceId is Guid openedWorkspaceId)
                {
                    try
                    {
                        await CloseWorkspaceAsync(host, openedWorkspaceId);
                        workspaceClosed = true;
                    }
                    catch (Exception exception)
                    {
                        runFailure = CombineFailures(runFailure, exception);
                    }
                }

                if (workspaceClosed
                    && options.Command == ScenarioCommand.Profile
                    && options.Profile == ProfileKind.GcDump)
                {
                    try
                    {
                        var collector = new DiagnosticCollector(frameworkRoot);
                        await collector.CollectGcDumpAsync(
                            host.ProcessId,
                            Path.Combine(outputDirectory, "heap-after-close.gcdump"),
                            CancellationToken.None);
                    }
                    catch (Exception exception)
                    {
                        runFailure = CombineFailures(runFailure, exception);
                    }
                }

                try
                {
                    await host.DisposeAsync();
                }
                catch (Exception exception)
                {
                    runFailure = CombineFailures(runFailure, exception);
                }
            }

            var shutdown = host.GetShutdownResult();
            var validation = await RunStateValidator.ValidateAsync(
                repository,
                repositoryRoot,
                stateDirectory,
                initialWorkspaceStateFiles,
                shutdown,
                CancellationToken.None);

            await ResultWriter.WriteRunValidationAsync(outputDirectory, validation, CancellationToken.None);
            if (!validation.Succeeded)
            {
                runFailure = CombineFailures(
                    runFailure,
                    new InvalidOperationException($"Run validation failed: {string.Join(" ", validation.Issues)}"));
            }

            runFailure?.Throw();
            await Console.Out.WriteLineAsync($"Results: {outputDirectory}");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }
        finally
        {
            DeleteExecutionDirectory(executionDirectory);
        }
    }

    private static async Task MeasureAsync(
        ScenarioOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        ToolInvocationRunner runner,
        RunEnvironmentInfo environment,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var results = new List<ScenarioRunResult>();
        foreach (var scenario in scenarios)
        {
            await Console.Out.WriteLineAsync($"Measuring {repository.Id}/{scenario.Id}");
            var startedAtUtc = DateTimeOffset.UtcNow;
            await runner.WarmUpAsync(scenario, options.Warmups, cancellationToken);
            var measurements = await runner.MeasureAsync(scenario, options.Iterations, cancellationToken);

            results.Add(new ScenarioRunResult
            {
                Repository = repository.Id,
                RepositorySize = repository.Size,
                Commit = repository.Commit,
                Scenario = scenario.Id,
                Tool = scenario.Tool,
                StartedAtUtc = startedAtUtc,
                Environment = environment,
                WarmupCount = options.Warmups,
                Measurements = measurements,
            });
        }

        await ResultWriter.WriteMeasurementsAsync(outputDirectory, results, cancellationToken);
    }

    private static async Task MeasureCancellationAsync(
        ScenarioOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        ToolInvocationRunner runner,
        RunEnvironmentInfo environment,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (scenarios.Count != 1)
        {
            throw new ArgumentException("Cancellation measurement requires exactly one scenario.");
        }

        var scenario = scenarios[0];
        await Console.Out.WriteLineAsync($"Measuring cancellation for {repository.Id}/{scenario.Id}");
        var startedAtUtc = DateTimeOffset.UtcNow;
        await runner.WarmUpAsync(scenario, options.Warmups, cancellationToken);
        var measurements = await runner.MeasureCancellationAsync(
            scenario,
            options.Iterations,
            options.CancellationDelay,
            cancellationToken);

        var result = new CancellationRunResult
        {
            Repository = repository.Id,
            RepositorySize = repository.Size,
            Commit = repository.Commit,
            Scenario = scenario.Id,
            Tool = scenario.Tool,
            StartedAtUtc = startedAtUtc,
            Environment = environment,
            WarmupCount = options.Warmups,
            CancellationDelay = options.CancellationDelay,
            Measurements = measurements,
        };

        await ResultWriter.WriteCancellationAsync(outputDirectory, result, cancellationToken);
        ValidateCancellationOutcomes(measurements);
    }

    private static void ValidateCancellationOutcomes(IReadOnlyList<CancellationMeasurement> measurements)
    {
        var ignoredIterations = measurements
            .Where(static measurement => measurement.Outcome == CancellationOutcome.CompletedAfterNotification)
            .Select(static measurement => measurement.Iteration)
            .ToArray();

        if (ignoredIterations.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The Host ignored protocol cancellation for iteration(s): {string.Join(", ", ignoredIterations)}. Complete cancellation evidence was written before this failure.");
    }

    private static async Task MeasureConcurrencyAsync(
        ScenarioOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        ScenarioHost host,
        Guid primaryWorkspaceId,
        string repositoryRoot,
        RunEnvironmentInfo environment,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (scenarios.Count != 1)
        {
            throw new ArgumentException(
                "Concurrency measurement requires exactly one query scenario.");
        }

        var scenario = scenarios[0];
        var definition = scenario.Concurrency
            ?? throw new ArgumentException(
                $"Scenario '{scenario.Id}' does not define concurrency settings.");
        var secondaryWorkspacePath = Path.Combine(
            repositoryRoot,
            definition.SecondaryWorkspacePath);

        await Console.Out.WriteLineAsync(
            $"Measuring concurrency {repository.Id}/{scenario.Id}");

        var secondaryWorkspaceId = await OpenWorkspaceAsync(
            host,
            secondaryWorkspacePath,
            repositoryRoot,
            cancellationToken,
            alias: "concurrency-secondary");

        try
        {
            var runner = new ConcurrencyRunner(
                host,
                primaryWorkspaceId,
                secondaryWorkspaceId,
                repositoryRoot);
            var startedAtUtc = DateTimeOffset.UtcNow;
            var execution = await runner.ExecuteAsync(
                scenario,
                options.Warmups,
                options.Iterations,
                options.Parallelism,
                cancellationToken);

            var result = new ConcurrencyRunResult
            {
                Repository = repository.Id,
                RepositorySize = repository.Size,
                Commit = repository.Commit,
                Scenario = scenario.Id,
                Tool = scenario.Tool,
                StartedAtUtc = startedAtUtc,
                Environment = environment,
                WarmupCount = options.Warmups,
                Parallelism = options.Parallelism,
                Batches = execution.Batches,
                MultiWorkspace = execution.MultiWorkspace,
            };

            await ResultWriter.WriteConcurrencyAsync(
                outputDirectory,
                result,
                cancellationToken);
        }
        finally
        {
            await CloseWorkspaceAsync(host, secondaryWorkspaceId);
        }
    }

    private static async Task MeasureDurableCommitsAsync(
        ScenarioOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        string hostPath,
        string repositoryRoot,
        RunEnvironmentInfo environment,
        string executionDirectory,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (scenarios.Count != 1)
        {
            throw new ArgumentException("Durable commit measurement requires exactly one mutation scenario.");
        }

        var scenario = scenarios[0];
        var workspacePath = Path.Combine(repositoryRoot, repository.WorkspacePath);
        var restorer = await RepositoryRestorer.CreateAsync(
            repositoryRoot,
            repository.Commit,
            cancellationToken);
        var initialWorkspaceStateFiles = RunStateValidator.CaptureWorkspaceStateFiles(repositoryRoot);
        var startedAtUtc = DateTimeOffset.UtcNow;

        for (var warmup = 1; warmup <= options.Warmups; warmup++)
        {
            await Console.Out.WriteLineAsync(
                $"Warming durable commit {repository.Id}/{scenario.Id} ({warmup}/{options.Warmups})");
            await RunDurableCommitIterationAsync(
                repository,
                scenario,
                hostPath,
                repositoryRoot,
                workspacePath,
                Path.Combine(executionDirectory, "state", $"warmup-{warmup}"),
                initialWorkspaceStateFiles,
                restorer,
                iteration: 0,
                diagnosticArtifact: null,
                cancellationToken);
        }

        var measurements = new List<DurableCommitMeasurement>();
        for (var iteration = 1; iteration <= options.Iterations; iteration++)
        {
            await Console.Out.WriteLineAsync(
                $"Measuring durable commit {repository.Id}/{scenario.Id} ({iteration}/{options.Iterations})");
            var measurement = await RunDurableCommitIterationAsync(
                repository,
                scenario,
                hostPath,
                repositoryRoot,
                workspacePath,
                Path.Combine(executionDirectory, "state", $"iteration-{iteration}"),
                initialWorkspaceStateFiles,
                restorer,
                iteration,
                options.CaptureTrace
                    ? Path.Combine(outputDirectory, $"commit-iteration-{iteration}.nettrace")
                    : null,
                cancellationToken);

            measurements.Add(measurement);
        }

        var result = new DurableCommitRunResult
        {
            Repository = repository.Id,
            RepositorySize = repository.Size,
            Commit = repository.Commit,
            Scenario = scenario.Id,
            MutationTool = scenario.Tool,
            StartedAtUtc = startedAtUtc,
            Environment = environment,
            WarmupCount = options.Warmups,
            Measurements = measurements,
        };

        await ResultWriter.WriteDurableCommitAsync(outputDirectory, result, cancellationToken);
    }

    private static async Task MeasureCommitCancellationsAsync(
        ScenarioOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        string hostPath,
        string repositoryRoot,
        RunEnvironmentInfo environment,
        string executionDirectory,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (scenarios.Count != 1)
        {
            throw new ArgumentException(
                "Commit-cancellation measurement requires exactly one mutation scenario.");
        }

        var scenario = scenarios[0];
        var workspacePath = Path.Combine(repositoryRoot, repository.WorkspacePath);
        var restorer = await RepositoryRestorer.CreateAsync(
            repositoryRoot,
            repository.Commit,
            cancellationToken);
        var initialWorkspaceStateFiles = RunStateValidator.CaptureWorkspaceStateFiles(
            repositoryRoot);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var boundaries = Enum.GetValues<CommitCancellationBoundary>();

        for (var warmup = 1; warmup <= options.Warmups; warmup++)
        {
            foreach (var boundary in boundaries)
            {
                await Console.Out.WriteLineAsync(
                    $"Warming commit cancellation {repository.Id}/{scenario.Id}/{boundary} ({warmup}/{options.Warmups})");
                await RunCommitCancellationIterationAsync(
                    repository,
                    scenario,
                    boundary,
                    hostPath,
                    repositoryRoot,
                    workspacePath,
                    Path.Combine(
                        executionDirectory,
                        "state",
                        $"warmup-{warmup}-{boundary}"),
                    initialWorkspaceStateFiles,
                    restorer,
                    iteration: 0,
                    cancellationToken);
            }
        }

        var measurements = new List<CommitCancellationMeasurement>();
        for (var iteration = 1; iteration <= options.Iterations; iteration++)
        {
            foreach (var boundary in boundaries)
            {
                await Console.Out.WriteLineAsync(
                    $"Measuring commit cancellation {repository.Id}/{scenario.Id}/{boundary} ({iteration}/{options.Iterations})");
                var measurement = await RunCommitCancellationIterationAsync(
                    repository,
                    scenario,
                    boundary,
                    hostPath,
                    repositoryRoot,
                    workspacePath,
                    Path.Combine(
                        executionDirectory,
                        "state",
                        $"iteration-{iteration}-{boundary}"),
                    initialWorkspaceStateFiles,
                    restorer,
                    iteration,
                    cancellationToken);

                measurements.Add(measurement);
            }
        }

        var result = new CommitCancellationRunResult
        {
            Repository = repository.Id,
            RepositorySize = repository.Size,
            Commit = repository.Commit,
            Scenario = scenario.Id,
            MutationTool = scenario.Tool,
            StartedAtUtc = startedAtUtc,
            Environment = environment,
            WarmupCount = options.Warmups,
            Measurements = measurements,
        };

        await ResultWriter.WriteCommitCancellationAsync(
            outputDirectory,
            result,
            cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The manual runner must preserve cancellation, rollback, workspace-close, Host-disposal, restoration, and validation failures so every cleanup step is attempted and all failures are reported together.")]
    private static async Task<CommitCancellationMeasurement> RunCommitCancellationIterationAsync(
        RepositoryDefinition repository,
        ScenarioDefinition scenario,
        CommitCancellationBoundary boundary,
        string hostPath,
        string repositoryRoot,
        string workspacePath,
        string stateDirectory,
        IReadOnlySet<string> initialWorkspaceStateFiles,
        RepositoryRestorer restorer,
        int iteration,
        CancellationToken cancellationToken)
    {
        await using var host = await ScenarioHost.StartAsync(
            hostPath,
            repositoryRoot,
            stateDirectory,
            pluginDirectory: null,
            cancellationToken);
        Guid? workspaceId = null;
        CommitCancellationRunner? runner = null;
        CommitCancellationExecution? execution = null;
        ExceptionDispatchInfo? runFailure = null;

        try
        {
            var openedWorkspaceId = await OpenWorkspaceAsync(
                host,
                workspacePath,
                repositoryRoot,
                cancellationToken);
            workspaceId = openedWorkspaceId;

            runner = new CommitCancellationRunner(
                host,
                openedWorkspaceId,
                repositoryRoot,
                stateDirectory);

            execution = await runner.ExecuteAsync(
                scenario,
                boundary,
                cancellationToken);
        }
        catch (Exception exception)
        {
            runFailure = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            if (runner?.HasOpenTransaction == true)
            {
                try
                {
                    await runner.RollbackAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    runFailure = CombineFailures(runFailure, exception);
                }
            }

            if (workspaceId is Guid openedWorkspaceId)
            {
                try
                {
                    await CloseWorkspaceAsync(host, openedWorkspaceId);
                }
                catch (Exception exception)
                {
                    runFailure = CombineFailures(runFailure, exception);
                }
            }

            try
            {
                await host.DisposeAsync();
            }
            catch (Exception exception)
            {
                runFailure = CombineFailures(runFailure, exception);
            }
        }

        RepositoryChangeSet? changes = null;
        double restorationMilliseconds = 0;
        try
        {
            changes = await restorer.CaptureChangesAsync(CancellationToken.None);
            restorationMilliseconds = await restorer.RestoreAsync(
                changes,
                CancellationToken.None);

            RunStateValidator.RestoreWorkspaceStateFiles(
                repositoryRoot,
                initialWorkspaceStateFiles);
        }
        catch (Exception exception)
        {
            runFailure = CombineFailures(runFailure, exception);
        }

        var shutdown = host.GetShutdownResult();
        var validation = await RunStateValidator.ValidateAsync(
            repository,
            repositoryRoot,
            stateDirectory,
            initialWorkspaceStateFiles,
            shutdown,
            CancellationToken.None);

        if (!validation.Succeeded)
        {
            runFailure = CombineFailures(
                runFailure,
                new InvalidOperationException(
                    $"Commit-cancellation validation failed: {string.Join(" ", validation.Issues)}"));
        }

        if (changes is not null)
        {
            ValidateCommitCancellationChanges(boundary, changes);
        }

        runFailure?.Throw();

        var completedExecution = execution
            ?? throw new InvalidOperationException(
                "Commit cancellation did not produce execution evidence.");
        var completedChanges = changes
            ?? throw new InvalidOperationException(
                "Commit cancellation did not produce a repository change set.");

        return new CommitCancellationMeasurement
        {
            Iteration = iteration,
            Boundary = boundary,
            ObservedPhase = completedExecution.ObservedPhase,
            StagingMilliseconds = completedExecution.StagingMilliseconds,
            PreviewMilliseconds = completedExecution.PreviewMilliseconds,
            CancellationNotificationMilliseconds =
                completedExecution.CancellationNotificationMilliseconds,
            CompletionAfterCancellationMilliseconds =
                completedExecution.CompletionAfterCancellationMilliseconds,
            SettlementMilliseconds =
                completedExecution.SettlementMilliseconds,
            OperationCanceled = completedExecution.OperationCanceled,
            Committed = completedExecution.Committed,
            PreviewDocumentCount = completedExecution.PreviewDocumentCount,
            PostCancellationPreviewDocumentCount =
                completedExecution.PostCancellationPreviewDocumentCount,
            RecoveryEvidence = completedExecution.RecoveryEvidence,
            RestorationMilliseconds = restorationMilliseconds,
            Files = completedChanges.Files,
            HostShutdown = shutdown,
            Validation = validation,
        };
    }

    private static void ValidateCommitCancellationChanges(
        CommitCancellationBoundary boundary,
        RepositoryChangeSet changes)
    {
        if (boundary == CommitCancellationBoundary.BeforeApplying
            && changes.Files.Count != 0)
        {
            throw new InvalidOperationException(
                "Pre-application cancellation changed repository files.");
        }

        if (boundary == CommitCancellationBoundary.AfterApplying
            && changes.Files.Count == 0)
        {
            throw new InvalidOperationException(
                "Post-application cancellation did not complete the durable commit.");
        }
    }

    private static async Task MeasureStateSequencesAsync(
        ScenarioOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        string hostPath,
        string repositoryRoot,
        RunEnvironmentInfo environment,
        string executionDirectory,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (scenarios.Count != 1)
        {
            throw new ArgumentException(
                "State-sequence measurement requires exactly one state-sequence scenario.");
        }

        var scenario = scenarios[0];
        var definition = scenario.StateSequence
            ?? throw new ArgumentException(
                $"Scenario '{scenario.Id}' does not define a state sequence.");
        var workspacePath = Path.Combine(repositoryRoot, repository.WorkspacePath);
        var restorer = await RepositoryRestorer.CreateAsync(
            repositoryRoot,
            repository.Commit,
            cancellationToken);
        var initialWorkspaceStateFiles = RunStateValidator.CaptureWorkspaceStateFiles(
            repositoryRoot);
        var startedAtUtc = DateTimeOffset.UtcNow;

        for (var warmup = 1; warmup <= options.Warmups; warmup++)
        {
            await Console.Out.WriteLineAsync(
                $"Warming state sequence {repository.Id}/{scenario.Id} ({warmup}/{options.Warmups})");
            await RunStateSequenceIterationAsync(
                repository,
                scenario,
                hostPath,
                repositoryRoot,
                workspacePath,
                Path.Combine(executionDirectory, "state", $"warmup-{warmup}"),
                initialWorkspaceStateFiles,
                restorer,
                iteration: 0,
                cancellationToken);
        }

        var measurements = new List<StateSequenceMeasurement>();
        for (var iteration = 1; iteration <= options.Iterations; iteration++)
        {
            await Console.Out.WriteLineAsync(
                $"Measuring state sequence {repository.Id}/{scenario.Id} ({iteration}/{options.Iterations})");
            var measurement = await RunStateSequenceIterationAsync(
                repository,
                scenario,
                hostPath,
                repositoryRoot,
                workspacePath,
                Path.Combine(executionDirectory, "state", $"iteration-{iteration}"),
                initialWorkspaceStateFiles,
                restorer,
                iteration,
                cancellationToken);

            measurements.Add(measurement);
        }

        var result = new StateSequenceRunResult
        {
            Repository = repository.Id,
            RepositorySize = repository.Size,
            Commit = repository.Commit,
            Scenario = scenario.Id,
            Kind = definition.Kind,
            StartedAtUtc = startedAtUtc,
            Environment = environment,
            WarmupCount = options.Warmups,
            Measurements = measurements,
        };

        await ResultWriter.WriteStateSequenceAsync(
            outputDirectory,
            result,
            cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The manual runner must preserve workload, workspace-close, Host-disposal, restoration, and validation failures so every cleanup step is attempted and all failures are reported together.")]
    private static async Task<StateSequenceMeasurement> RunStateSequenceIterationAsync(
        RepositoryDefinition repository,
        ScenarioDefinition scenario,
        string hostPath,
        string repositoryRoot,
        string workspacePath,
        string stateDirectory,
        IReadOnlySet<string> initialWorkspaceStateFiles,
        RepositoryRestorer restorer,
        int iteration,
        CancellationToken cancellationToken)
    {
        var stateSequence = scenario.StateSequence
            ?? throw new InvalidOperationException(
                $"Scenario '{scenario.Id}' does not define a state sequence.");

        var externalWildcardDefinition = stateSequence.WatcherStress?.ExternalWildcard;
        ExternalWildcardWorkspaceSetup? setup = null;
        if (externalWildcardDefinition is not null)
        {
            setup = ExternalWildcardWorkspaceSetup.Apply(
                repositoryRoot,
                externalWildcardDefinition,
                cancellationToken);
        }

        using var externalWildcardSetup = setup;

        await using var host = await ScenarioHost.StartAsync(
            hostPath,
            repositoryRoot,
            stateDirectory,
            pluginDirectory: null,
            cancellationToken);
        Guid? workspaceId = null;
        StateSequenceExecution? execution = null;
        ExceptionDispatchInfo? runFailure = null;

        try
        {
            var openedWorkspaceId = await OpenWorkspaceAsync(
                host,
                workspacePath,
                repositoryRoot,
                cancellationToken);
            workspaceId = openedWorkspaceId;

            var runner = new StateSequenceRunner(
                host,
                openedWorkspaceId,
                repositoryRoot,
                workspacePath);

            execution = await runner.ExecuteAsync(scenario, cancellationToken);
        }
        catch (Exception exception)
        {
            runFailure = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            if (workspaceId is Guid openedWorkspaceId)
            {
                try
                {
                    await CloseWorkspaceAsync(host, openedWorkspaceId);
                }
                catch (Exception exception)
                {
                    runFailure = CombineFailures(runFailure, exception);
                }
            }

            try
            {
                await host.DisposeAsync();
            }
            catch (Exception exception)
            {
                runFailure = CombineFailures(runFailure, exception);
            }
        }

        try
        {
            externalWildcardSetup?.Restore();
        }
        catch (Exception exception)
        {
            runFailure = CombineFailures(runFailure, exception);
        }

        RepositoryChangeSet? changes = null;
        double restorationMilliseconds = 0;
        try
        {
            changes = await restorer.CaptureChangesAsync(CancellationToken.None);
            restorationMilliseconds = await restorer.RestoreAsync(
                changes,
                CancellationToken.None);

            RunStateValidator.RestoreWorkspaceStateFiles(
                repositoryRoot,
                initialWorkspaceStateFiles);
        }
        catch (Exception exception)
        {
            runFailure = CombineFailures(runFailure, exception);
        }

        var shutdown = host.GetShutdownResult();
        var validation = await RunStateValidator.ValidateAsync(
            repository,
            repositoryRoot,
            stateDirectory,
            initialWorkspaceStateFiles,
            shutdown,
            CancellationToken.None);

        if (!validation.Succeeded)
        {
            runFailure = CombineFailures(
                runFailure,
                new InvalidOperationException(
                    $"State-sequence validation failed: {string.Join(" ", validation.Issues)}"));
        }

        if (changes is not null)
        {
            ValidateStateSequenceChanges(scenario, changes);
        }

        runFailure?.Throw();

        var completedExecution = execution
            ?? throw new InvalidOperationException(
                "State sequence did not produce execution evidence.");
        var completedChanges = changes
            ?? throw new InvalidOperationException(
                "State sequence did not produce a repository change set.");

        return new StateSequenceMeasurement
        {
            Iteration = iteration,
            Steps = completedExecution.Steps,
            ExternalCommand = completedExecution.ExternalCommand,
            WatcherStress = completedExecution.WatcherStress,
            RestorationMilliseconds = restorationMilliseconds,
            Files = completedChanges.Files,
            HostShutdown = shutdown,
            Validation = validation,
        };
    }

    private static void ValidateStateSequenceChanges(
        ScenarioDefinition scenario,
        RepositoryChangeSet changes)
    {
        var definition = scenario.StateSequence
            ?? throw new InvalidOperationException(
                $"Scenario '{scenario.Id}' does not define a state sequence.");

        if ((definition.Kind is StateSequenceKind.ExternalReload
                or StateSequenceKind.LiveBuild)
            && changes.Files.Count != 0)
        {
            throw new InvalidOperationException(
                $"{definition.Kind} sequence left repository changes after completion.");
        }

        if (definition.Kind == StateSequenceKind.MultiRevisionCommit
            && changes.Files.Count == 0)
        {
            throw new InvalidOperationException(
                "Multi-revision sequence committed without changing repository files.");
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The manual runner must preserve workload, workspace-close, Host-disposal, restoration, and validation failures so every cleanup step is attempted and all failures are reported together.")]
    private static async Task<DurableCommitMeasurement> RunDurableCommitIterationAsync(
        RepositoryDefinition repository,
        ScenarioDefinition scenario,
        string hostPath,
        string repositoryRoot,
        string workspacePath,
        string stateDirectory,
        IReadOnlySet<string> initialWorkspaceStateFiles,
        RepositoryRestorer restorer,
        int iteration,
        string? diagnosticArtifact,
        CancellationToken cancellationToken)
    {
        await using var host = await ScenarioHost.StartAsync(
            hostPath,
            repositoryRoot,
            stateDirectory,
            pluginDirectory: null,
            cancellationToken);
        Guid? workspaceId = null;
        DurableCommitRunner? runner = null;
        DurableCommitExecution? execution = null;
        ExceptionDispatchInfo? runFailure = null;

        try
        {
            var openedWorkspaceId = await OpenWorkspaceAsync(host, workspacePath, repositoryRoot, cancellationToken);
            workspaceId = openedWorkspaceId;
            runner = new DurableCommitRunner(host, openedWorkspaceId, repositoryRoot);
            if (diagnosticArtifact is null)
            {
                execution = await runner.ExecuteAsync(scenario, cancellationToken);
            }
            else
            {
                var preparation = await runner.PrepareAsync(scenario, cancellationToken);
                Directory.CreateDirectory(Path.GetDirectoryName(diagnosticArtifact)!);
                try
                {
                    await using var traceCollection = await TraceCollection.StartAsync(
                        host.ProcessId,
                        diagnosticArtifact,
                        cancellationToken);

                    try
                    {
                        execution = await runner.CommitAsync(preparation, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        runFailure = CombineFailures(runFailure, exception);
                    }
                }
                catch (Exception exception)
                {
                    runFailure = CombineFailures(runFailure, exception);
                }
            }
        }
        catch (Exception exception)
        {
            runFailure = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            if (runFailure is not null && runner is not null)
            {
                try
                {
                    await runner.RollbackAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    runFailure = CombineFailures(runFailure, exception);
                }
            }

            if (workspaceId is Guid openedWorkspaceId)
            {
                try
                {
                    await CloseWorkspaceAsync(host, openedWorkspaceId);
                }
                catch (Exception exception)
                {
                    runFailure = CombineFailures(runFailure, exception);
                }
            }

            try
            {
                await host.DisposeAsync();
            }
            catch (Exception exception)
            {
                runFailure = CombineFailures(runFailure, exception);
            }
        }

        RepositoryChangeSet? changes = null;
        double restorationMilliseconds = 0;
        try
        {
            changes = await restorer.CaptureChangesAsync(CancellationToken.None);
            restorationMilliseconds = await restorer.RestoreAsync(changes, CancellationToken.None);
            RunStateValidator.RestoreWorkspaceStateFiles(
                repositoryRoot,
                initialWorkspaceStateFiles);
        }
        catch (Exception exception)
        {
            runFailure = CombineFailures(runFailure, exception);
        }

        var shutdown = host.GetShutdownResult();
        var validation = await RunStateValidator.ValidateAsync(
            repository,
            repositoryRoot,
            stateDirectory,
            initialWorkspaceStateFiles,
            shutdown,
            CancellationToken.None);
        if (!validation.Succeeded)
        {
            runFailure = CombineFailures(
                runFailure,
                new InvalidOperationException(
                    $"Durable commit validation failed: {string.Join(" ", validation.Issues)}"));
        }

        if (execution is not null && execution.PreviewDocumentCount == 0)
        {
            runFailure = CombineFailures(
                runFailure,
                new InvalidOperationException(
                    "transaction-preview reported no changed documents for the staged mutation."));
        }

        if (execution is not null
            && changes is not null
            && changes.Files.Count == 0)
        {
            runFailure = CombineFailures(
                runFailure,
                new InvalidOperationException(
                    "transaction-commit reported success without changing any repository files."));
        }

        runFailure?.Throw();

        var completedExecution = execution
            ?? throw new InvalidOperationException("Durable commit execution did not produce a measurement.");
        var completedChanges = changes
            ?? throw new InvalidOperationException("Durable commit execution did not produce a repository change set.");

        return new DurableCommitMeasurement
        {
            Iteration = iteration,
            StagingMilliseconds = completedExecution.StagingMilliseconds,
            PreviewMilliseconds = completedExecution.PreviewMilliseconds,
            CommitMilliseconds = completedExecution.CommitMilliseconds,
            CommitHostCpuMilliseconds = completedExecution.CommitHostCpuMilliseconds,
            RestorationMilliseconds = restorationMilliseconds,
            WorkingSetBytes = completedExecution.WorkingSetBytes,
            PeakWorkingSetBytes = completedExecution.PeakWorkingSetBytes,
            CommitMemory = completedExecution.CommitMemory,
            CommitResponseBytes = completedExecution.CommitResponseBytes,
            CommitResponseSha256 = completedExecution.CommitResponseSha256,
            PreviewDocumentCount = completedExecution.PreviewDocumentCount,
            Files = completedChanges.Files,
            HostShutdown = shutdown,
            Validation = validation,
            DiagnosticArtifact = diagnosticArtifact,
            PhaseSummary = diagnosticArtifact is not null && File.Exists(diagnosticArtifact)
                ? PhaseTraceAnalyzer.Analyze(diagnosticArtifact)
                : [],
            AtomicFileCommitRetries = diagnosticArtifact is not null && File.Exists(diagnosticArtifact)
                ? PhaseTraceAnalyzer.AnalyzeAtomicFileCommitRetries(diagnosticArtifact)
                : null,
        };
    }

    private static async Task MeasureConflictsAsync(
        ScenarioOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        string hostPath,
        string repositoryRoot,
        RunEnvironmentInfo environment,
        string executionDirectory,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (scenarios.Count != 1)
        {
            throw new ArgumentException(
                "Conflict measurement requires exactly one conflict scenario.");
        }

        var scenario = scenarios[0];
        var conflict = scenario.Conflict
            ?? throw new ArgumentException(
                $"Scenario '{scenario.Id}' does not define a controlled conflict.");
        var workspacePath = Path.Combine(repositoryRoot, repository.WorkspacePath);
        var restorer = await RepositoryRestorer.CreateAsync(
            repositoryRoot,
            repository.Commit,
            cancellationToken);

        var initialWorkspaceStateFiles = RunStateValidator.CaptureWorkspaceStateFiles(
            repositoryRoot);

        var startedAtUtc = DateTimeOffset.UtcNow;

        for (var warmup = 1; warmup <= options.Warmups; warmup++)
        {
            await Console.Out.WriteLineAsync(
                $"Warming conflict {repository.Id}/{scenario.Id} ({warmup}/{options.Warmups})");
            await RunConflictIterationAsync(
                repository,
                scenario,
                hostPath,
                repositoryRoot,
                workspacePath,
                Path.Combine(executionDirectory, "state", $"warmup-{warmup}"),
                initialWorkspaceStateFiles,
                restorer,
                iteration: 0,
                cancellationToken);
        }

        var measurements = new List<ConflictMeasurement>();
        for (var iteration = 1; iteration <= options.Iterations; iteration++)
        {
            await Console.Out.WriteLineAsync(
                $"Measuring conflict {repository.Id}/{scenario.Id} ({iteration}/{options.Iterations})");
            var measurement = await RunConflictIterationAsync(
                repository,
                scenario,
                hostPath,
                repositoryRoot,
                workspacePath,
                Path.Combine(executionDirectory, "state", $"iteration-{iteration}"),
                initialWorkspaceStateFiles,
                restorer,
                iteration,
                cancellationToken);

            measurements.Add(measurement);
        }

        var result = new ConflictRunResult
        {
            Repository = repository.Id,
            RepositorySize = repository.Size,
            Commit = repository.Commit,
            Scenario = scenario.Id,
            Mode = conflict.Mode,
            StartedAtUtc = startedAtUtc,
            Environment = environment,
            WarmupCount = options.Warmups,
            Measurements = measurements,
        };

        await ResultWriter.WriteConflictAsync(
            outputDirectory,
            result,
            cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The manual runner must preserve conflict, shutdown, evidence, restoration, and validation failures so every cleanup step is attempted and all failures are reported together.")]
    private static async Task<ConflictMeasurement> RunConflictIterationAsync(
        RepositoryDefinition repository,
        ScenarioDefinition scenario,
        string hostPath,
        string repositoryRoot,
        string workspacePath,
        string stateDirectory,
        IReadOnlySet<string> initialWorkspaceStateFiles,
        RepositoryRestorer restorer,
        int iteration,
        CancellationToken cancellationToken)
    {
        var conflict = scenario.Conflict
            ?? throw new InvalidOperationException(
                $"Scenario '{scenario.Id}' does not define a controlled conflict.");
        await using var host = await ScenarioHost.StartAsync(
            hostPath,
            repositoryRoot,
            stateDirectory,
            pluginDirectory: null,
            cancellationToken);
        Guid? workspaceId = null;
        ConflictExecution? execution = null;
        ExceptionDispatchInfo? runFailure = null;

        try
        {
            var openedWorkspaceId = await OpenWorkspaceAsync(
                host,
                workspacePath,
                repositoryRoot,
                cancellationToken);
            workspaceId = openedWorkspaceId;
            var durableRunner = new DurableCommitRunner(
                host,
                openedWorkspaceId,
                repositoryRoot);
            var preparation = await durableRunner.PrepareAsync(
                scenario,
                cancellationToken);
            var conflictRunner = new ConflictRunner(
                host,
                openedWorkspaceId,
                repositoryRoot,
                stateDirectory);
            execution = await conflictRunner.ExecuteAsync(
                scenario,
                preparation,
                cancellationToken);
        }
        catch (Exception exception)
        {
            runFailure = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            if (workspaceId is Guid openedWorkspaceId
                && conflict.Mode == ConflictMode.PreWriteDrift)
            {
                try
                {
                    await CloseWorkspaceAsync(host, openedWorkspaceId);
                }
                catch (Exception exception)
                {
                    runFailure = CombineFailures(runFailure, exception);
                }
            }

            try
            {
                await host.DisposeAsync();
            }
            catch (Exception exception)
            {
                runFailure = CombineFailures(runFailure, exception);
            }
        }

        RecoveryEvidence? recoveryEvidence = null;
        RepositoryChangeSet? changes = null;
        double restorationMilliseconds = 0;
        try
        {
            recoveryEvidence = await RecoveryEvidenceReader.ReadAsync(
                stateDirectory,
                CancellationToken.None);
            changes = await restorer.CaptureChangesAsync(CancellationToken.None);
            ValidateConflictEvidence(
                conflict.Mode,
                execution,
                changes,
                recoveryEvidence,
                repositoryRoot);
        }
        catch (Exception exception)
        {
            runFailure = CombineFailures(runFailure, exception);
        }

        try
        {
            changes ??= await restorer.CaptureChangesAsync(CancellationToken.None);
            restorationMilliseconds = await restorer.RestoreAsync(
                changes,
                CancellationToken.None);
            ClearDirectory(stateDirectory);
            RunStateValidator.RestoreWorkspaceStateFiles(
                repositoryRoot,
                initialWorkspaceStateFiles);
        }
        catch (Exception exception)
        {
            runFailure = CombineFailures(runFailure, exception);
        }

        var shutdown = host.GetShutdownResult();
        var validation = await RunStateValidator.ValidateAsync(
            repository,
            repositoryRoot,
            stateDirectory,
            initialWorkspaceStateFiles,
            shutdown,
            CancellationToken.None);
        if (!validation.Succeeded)
        {
            runFailure = CombineFailures(
                runFailure,
                new InvalidOperationException(
                    $"Conflict validation failed: {string.Join(" ", validation.Issues)}"));
        }

        runFailure?.Throw();
        var completedExecution = execution
            ?? throw new InvalidOperationException(
                "Conflict execution did not produce a measurement.");
        var completedChanges = changes
            ?? throw new InvalidOperationException(
                "Conflict execution did not produce a repository change set.");
        var completedRecoveryEvidence = recoveryEvidence
            ?? throw new InvalidOperationException(
                "Conflict execution did not produce recovery evidence.");

        return new ConflictMeasurement
        {
            Iteration = iteration,
            StagingMilliseconds = completedExecution.StagingMilliseconds,
            PreviewMilliseconds = completedExecution.PreviewMilliseconds,
            CommitMilliseconds = completedExecution.CommitMilliseconds,
            ConflictDetectionMilliseconds = completedExecution.ConflictDetectionMilliseconds,
            RecoveryMilliseconds = completedExecution.RecoveryMilliseconds,
            RestorationMilliseconds = restorationMilliseconds,
            ErrorCode = completedExecution.ErrorCode,
            Continuation = completedExecution.Continuation,
            ExternalMutation = completedExecution.ExternalMutation,
            FilesBeforeRestoration = completedChanges.Files,
            RecoveryState = completedRecoveryEvidence.State,
            RecoveryArtifactCount = completedRecoveryEvidence.ArtifactCount,
            HostShutdown = shutdown,
            Validation = validation,
        };
    }

    private static async Task MeasureCrashRecoveriesAsync(
        ScenarioOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        string hostPath,
        string repositoryRoot,
        RunEnvironmentInfo environment,
        string executionDirectory,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (scenarios.Count != 1)
        {
            throw new ArgumentException(
                "Crash recovery measurement requires exactly one mutation scenario.");
        }

        var scenario = scenarios[0];
        var workspacePath = Path.Combine(repositoryRoot, repository.WorkspacePath);
        var restorer = await RepositoryRestorer.CreateAsync(
            repositoryRoot,
            repository.Commit,
            cancellationToken);
        var initialWorkspaceStateFiles = RunStateValidator.CaptureWorkspaceStateFiles(
            repositoryRoot);
        var startedAtUtc = DateTimeOffset.UtcNow;

        for (var warmup = 1; warmup <= options.Warmups; warmup++)
        {
            await Console.Out.WriteLineAsync(
                $"Warming crash recovery {repository.Id}/{scenario.Id} ({warmup}/{options.Warmups})");
            await RunCrashRecoveryIterationAsync(
                repository,
                scenario,
                hostPath,
                repositoryRoot,
                workspacePath,
                Path.Combine(executionDirectory, "state", $"warmup-{warmup}"),
                initialWorkspaceStateFiles,
                restorer,
                iteration: 0,
                cancellationToken);
        }

        var measurements = new List<CrashRecoveryMeasurement>();
        for (var iteration = 1; iteration <= options.Iterations; iteration++)
        {
            await Console.Out.WriteLineAsync(
                $"Measuring crash recovery {repository.Id}/{scenario.Id} ({iteration}/{options.Iterations})");
            var measurement = await RunCrashRecoveryIterationAsync(
                repository,
                scenario,
                hostPath,
                repositoryRoot,
                workspacePath,
                Path.Combine(executionDirectory, "state", $"iteration-{iteration}"),
                initialWorkspaceStateFiles,
                restorer,
                iteration,
                cancellationToken);

            measurements.Add(measurement);
        }

        var result = new CrashRecoveryRunResult
        {
            Repository = repository.Id,
            RepositorySize = repository.Size,
            Commit = repository.Commit,
            Scenario = scenario.Id,
            MutationTool = scenario.Tool,
            StartedAtUtc = startedAtUtc,
            Environment = environment,
            WarmupCount = options.Warmups,
            Measurements = measurements,
        };

        await ResultWriter.WriteCrashRecoveryAsync(
            outputDirectory,
            result,
            cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The manual runner must preserve interruption, restart, shutdown, restoration, and validation failures so every cleanup step is attempted and all failures are reported together.")]
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Both Host instances are retained in nullable locals and disposed in the unconditional finally block; the interrupted Host may already have been disposed by deliberate termination, which is idempotent.")]
    private static async Task<CrashRecoveryMeasurement> RunCrashRecoveryIterationAsync(
        RepositoryDefinition repository,
        ScenarioDefinition scenario,
        string hostPath,
        string repositoryRoot,
        string workspacePath,
        string stateDirectory,
        IReadOnlySet<string> initialWorkspaceStateFiles,
        RepositoryRestorer restorer,
        int iteration,
        CancellationToken cancellationToken)
    {
        ScenarioHost? interruptedHost = null;
        ScenarioHost? recoveryHost = null;
        CrashRecoveryInterruption? interruption = null;
        RepositoryChangeSet? filesBeforeRecovery = null;
        RepositoryChangeSet? filesAfterRecovery = null;
        RunValidationResult? validation = null;
        ExceptionDispatchInfo? runFailure = null;
        Guid? recoveryWorkspaceId = null;
        double recoveryStartupMilliseconds = 0;
        double workspaceReopenMilliseconds = 0;

        try
        {
            interruptedHost = await ScenarioHost.StartAsync(
                hostPath,
                repositoryRoot,
                stateDirectory,
                pluginDirectory: null,
                cancellationToken);

            var workspaceId = await OpenWorkspaceAsync(
                interruptedHost,
                workspacePath,
                repositoryRoot,
                cancellationToken);

            var durableRunner = new DurableCommitRunner(
                interruptedHost,
                workspaceId,
                repositoryRoot);

            var preparation = await durableRunner.PrepareAsync(
                scenario,
                cancellationToken);

            var crashRunner = new CrashRecoveryRunner(
                interruptedHost,
                workspaceId,
                repositoryRoot,
                stateDirectory);

            interruption = await crashRunner.InterruptAsync(
                preparation,
                scenario.CrashAfterOperation,
                cancellationToken);

            filesBeforeRecovery = await restorer.CaptureChangesAsync(
                cancellationToken);

            ValidateCrashInterruption(
                interruption,
                filesBeforeRecovery,
                repositoryRoot,
                scenario.CrashAfterOperation);

            var recoveryStartupStopwatch = Stopwatch.StartNew();
            recoveryHost = await ScenarioHost.StartAsync(
                hostPath,
                repositoryRoot,
                stateDirectory,
                pluginDirectory: null,
                cancellationToken);

            recoveryStartupStopwatch.Stop();
            recoveryStartupMilliseconds = recoveryStartupStopwatch.Elapsed.TotalMilliseconds;

            var workspaceReopenStopwatch = Stopwatch.StartNew();
            var openedRecoveryWorkspaceId = await OpenWorkspaceAsync(
                recoveryHost,
                workspacePath,
                repositoryRoot,
                cancellationToken);
            recoveryWorkspaceId = openedRecoveryWorkspaceId;

            workspaceReopenStopwatch.Stop();
            workspaceReopenMilliseconds = workspaceReopenStopwatch.Elapsed.TotalMilliseconds;

            await CloseWorkspaceAsync(recoveryHost, openedRecoveryWorkspaceId);
            recoveryWorkspaceId = null;
            await recoveryHost.DisposeAsync();

            var evidenceAfterRecovery = await RecoveryEvidenceReader.ReadAsync(
                stateDirectory,
                cancellationToken);

            filesAfterRecovery = await restorer.CaptureChangesAsync(
                cancellationToken);

            ValidateCrashRecovery(evidenceAfterRecovery, filesAfterRecovery);
            ValidateCrashWorkspaceStateFiles(
                repositoryRoot,
                initialWorkspaceStateFiles);
        }
        catch (Exception exception)
        {
            runFailure = CombineFailures(runFailure, exception);
        }
        finally
        {
            if (recoveryWorkspaceId is Guid openedRecoveryWorkspaceId && recoveryHost is not null)
            {
                try
                {
                    await CloseWorkspaceAsync(recoveryHost, openedRecoveryWorkspaceId);
                }
                catch (Exception exception)
                {
                    runFailure = CombineFailures(runFailure, exception);
                }
            }

            if (recoveryHost is not null)
            {
                try
                {
                    await recoveryHost.DisposeAsync();
                }
                catch (Exception exception)
                {
                    runFailure = CombineFailures(runFailure, exception);
                }
            }

            if (interruptedHost is not null)
            {
                try
                {
                    await interruptedHost.DisposeAsync();
                }
                catch (Exception exception)
                {
                    runFailure = CombineFailures(runFailure, exception);
                }
            }
        }

        double runnerCleanupMilliseconds = 0;
        try
        {
            filesAfterRecovery ??= await restorer.CaptureChangesAsync(
                CancellationToken.None);

            runnerCleanupMilliseconds = await restorer.RestoreAsync(
                filesAfterRecovery,
                CancellationToken.None);

            ClearDirectory(stateDirectory);
            RunStateValidator.RestoreWorkspaceStateFiles(
                repositoryRoot,
                initialWorkspaceStateFiles);
        }
        catch (Exception exception)
        {
            runFailure = CombineFailures(runFailure, exception);
        }

        if (recoveryHost is not null)
        {
            try
            {
                validation = await RunStateValidator.ValidateAsync(
                    repository,
                    repositoryRoot,
                    stateDirectory,
                    initialWorkspaceStateFiles,
                    recoveryHost.GetShutdownResult(),
                    CancellationToken.None);

                if (!validation.Succeeded)
                {
                    runFailure = CombineFailures(
                        runFailure,
                        new InvalidOperationException(
                            $"Crash recovery validation failed: {string.Join(" ", validation.Issues)}"));
                }
            }
            catch (Exception exception)
            {
                runFailure = CombineFailures(runFailure, exception);
            }
        }

        runFailure?.Throw();

        var completedInterruption = interruption
            ?? throw new InvalidOperationException(
                "Crash recovery did not produce interruption evidence.");
        var completedFilesBeforeRecovery = filesBeforeRecovery
            ?? throw new InvalidOperationException(
                "Crash recovery did not capture the partially applied repository state.");
        var completedRecoveryHost = recoveryHost
            ?? throw new InvalidOperationException(
                "Crash recovery did not start a fresh Host.");
        var completedValidation = validation
            ?? throw new InvalidOperationException(
                "Crash recovery did not produce final validation.");

        return new CrashRecoveryMeasurement
        {
            Iteration = iteration,
            StagingMilliseconds = completedInterruption.StagingMilliseconds,
            PreviewMilliseconds = completedInterruption.PreviewMilliseconds,
            InterruptionMilliseconds = completedInterruption.InterruptionMilliseconds,
            RecoveryStartupMilliseconds = recoveryStartupMilliseconds,
            WorkspaceReopenMilliseconds = workspaceReopenMilliseconds,
            RunnerCleanupMilliseconds = runnerCleanupMilliseconds,
            AppliedTargetPath = Path.GetRelativePath(
                repositoryRoot,
                completedInterruption.AppliedTargetPath),
            FilesBeforeRecovery = completedFilesBeforeRecovery.Files,
            PreparedRecoveryState = completedInterruption.RecoveryEvidence.State,
            PreparedRecoveryArtifactCount = completedInterruption.RecoveryEvidence.ArtifactCount,
            InterruptedHostShutdown = completedInterruption.HostShutdown,
            RecoveryHostShutdown = completedRecoveryHost.GetShutdownResult(),
            Validation = completedValidation,
        };
    }

    private static void ValidateCrashInterruption(
        CrashRecoveryInterruption interruption,
        RepositoryChangeSet filesBeforeRecovery,
        string repositoryRoot,
        DurableCommitFileOperation? requiredOperation)
    {
        if (!interruption.HostShutdown.ForcedTermination)
        {
            throw new InvalidOperationException(
                "The interrupted Host did not record deliberate forced termination.");
        }

        if (filesBeforeRecovery.Files.Count == 0)
        {
            throw new InvalidOperationException(
                "Host termination occurred before any repository mutation was observable.");
        }

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var appliedTargetPath = Path.GetFullPath(interruption.AppliedTargetPath);
        var appliedTarget = filesBeforeRecovery.Files.FirstOrDefault(file =>
            string.Equals(
                Path.GetFullPath(Path.Combine(repositoryRoot, file.Path)),
                appliedTargetPath,
                pathComparison));

        if (appliedTarget is null)
        {
            throw new InvalidOperationException(
                "The repository snapshot did not contain the mutation observed before Host termination.");
        }

        if (requiredOperation is not null
            && appliedTarget.Operation != requiredOperation)
        {
            throw new InvalidOperationException(
                $"The observed crash target was {appliedTarget.Operation} instead of the required {requiredOperation} operation.");
        }
    }

    private static void ValidateCrashRecovery(
        RecoveryEvidence recoveryEvidence,
        RepositoryChangeSet filesAfterRecovery)
    {
        if (recoveryEvidence.State is not null
            || recoveryEvidence.ArtifactCount != 0)
        {
            throw new InvalidOperationException(
                "Fresh-Host startup left unfinished recovery state.");
        }

        if (filesAfterRecovery.Files.Count != 0)
        {
            throw new InvalidOperationException(
                "Fresh-Host startup recovery did not restore the pinned repository state.");
        }
    }

    private static void ValidateCrashWorkspaceStateFiles(
        string repositoryRoot,
        IReadOnlySet<string> initialWorkspaceStateFiles)
    {
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        var expectedLockPath = Path.GetFullPath(
            Path.Combine(
                repositoryRoot,
                ".vs",
                "roslyn-workbench-mcp",
                "locks",
                "commit.lock"));

        var finalWorkspaceStateFiles = RunStateValidator.CaptureWorkspaceStateFiles(
            repositoryRoot);

        var unexpectedPaths = new List<string>();
        foreach (var path in finalWorkspaceStateFiles.Except(
            initialWorkspaceStateFiles,
            pathComparer))
        {
            if (!string.Equals(path, expectedLockPath, pathComparison))
            {
                unexpectedPaths.Add(path);
            }
        }

        if (unexpectedPaths.Count != 0)
        {
            throw new InvalidOperationException(
                $"Fresh-Host recovery left unexpected workspace state files: {string.Join(", ", unexpectedPaths)}.");
        }
    }

    private static void ValidateConflictEvidence(
        ConflictMode mode,
        ConflictExecution? execution,
        RepositoryChangeSet changes,
        RecoveryEvidence recoveryEvidence,
        string repositoryRoot)
    {
        if (execution is null)
        {
            return;
        }

        var expectedPath = Path.GetFullPath(execution.ExternalMutation.Path);
        var actualPath = changes.Files.Count == 1
            ? Path.GetFullPath(Path.Combine(repositoryRoot, changes.Files[0].Path))
            : null;
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (changes.Files.Count != 1
            || !string.Equals(
                actualPath,
                expectedPath,
                pathComparison))
        {
            var changedPaths = changes.Files.Count == 0
                ? "<none>"
                : string.Join(", ", changes.Files.Select(static file => file.Path));

            throw new InvalidOperationException(
                $"Conflict recovery expected only '{expectedPath}' to remain modified but observed: {changedPaths}.");
        }

        var actualSha256 = HashFile(execution.ExternalMutation.Path);
        if (!string.Equals(
            actualSha256,
            execution.ExternalMutation.ExternalSha256,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Conflict recovery overwrote or reverted the externally changed file.");
        }

        if (mode == ConflictMode.PreWriteDrift
            && (recoveryEvidence.State is not null
                || recoveryEvidence.ArtifactCount != 0))
        {
            throw new InvalidOperationException(
                "Pre-write drift created recovery state before any durable write.");
        }

        if (mode == ConflictMode.DuringApplication
            && (!string.Equals(
                recoveryEvidence.State,
                "RecoveryConflict",
                StringComparison.Ordinal)
                || recoveryEvidence.ArtifactCount == 0))
        {
            throw new InvalidOperationException(
                "In-progress conflict did not retain the expected RecoveryConflict evidence.");
        }
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
    }

    private static void ClearDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static async Task ProfileAsync(
        ScenarioOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        ScenarioHost host,
        ToolInvocationRunner runner,
        RunEnvironmentInfo environment,
        string frameworkRoot,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        await DiagnosticCollector.EnsureToolsRestoredAsync(frameworkRoot, cancellationToken);
        Directory.CreateDirectory(outputDirectory);
        var collector = new DiagnosticCollector(frameworkRoot);
        var artifactPath = Path.Combine(outputDirectory, GetArtifactName(options.Profile));
        var startedAtUtc = DateTimeOffset.UtcNow;
        var beforeProfile = host.CaptureSnapshot();
        int invocationCount;
        ProfileInvocationTiming? invocationTiming = null;

        if (options.Profile == ProfileKind.GcDump)
        {
            invocationCount = 0;
            foreach (var scenario in scenarios)
            {
                await runner.WarmUpAsync(scenario, options.Warmups, cancellationToken);
                await runner.RunCountAsync(scenario, options.Iterations, cancellationToken);
                invocationCount += options.Iterations;
            }

            await collector.CollectGcDumpAsync(host.ProcessId, artifactPath, cancellationToken);
        }
        else
        {
            foreach (var scenario in scenarios)
            {
                await runner.WarmUpAsync(scenario, options.Warmups, cancellationToken);
            }

            IReadOnlyList<double> elapsedMilliseconds;
            if (options.Profile == ProfileKind.Trace)
            {
                await using var traceCollection = await TraceCollection.StartAsync(
                    host.ProcessId,
                    artifactPath,
                    cancellationToken);

                elapsedMilliseconds = await runner.RunSequenceForMinimumDurationAsync(
                    scenarios,
                    options.ProfileDuration,
                    cancellationToken);

                await traceCollection.StopAsync(cancellationToken);
            }
            else
            {
                using var diagnosticProcess = collector.StartDurationProfile(
                    options.Profile,
                    host.ProcessId,
                    options.ProfileDuration,
                    artifactPath);
                try
                {
                    var standardOutput = diagnosticProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                    var standardError = diagnosticProcess.StandardError.ReadToEndAsync(cancellationToken);
                    await DiagnosticCollector.WaitForCollectionStartAsync(
                        diagnosticProcess,
                        cancellationToken);

                    elapsedMilliseconds = await runner.RunSequenceUntilExitAsync(
                        scenarios,
                        diagnosticProcess,
                        cancellationToken);

                    await diagnosticProcess.WaitForExitAsync(cancellationToken);

                    if (diagnosticProcess.ExitCode != 0)
                    {
                        throw new InvalidOperationException(
                            $"Diagnostic collection failed with exit code {diagnosticProcess.ExitCode}.{Environment.NewLine}{await standardError}{await standardOutput}");
                    }
                }
                finally
                {
                    if (!diagnosticProcess.HasExited)
                    {
                        diagnosticProcess.Kill(entireProcessTree: true);
                        await diagnosticProcess.WaitForExitAsync(CancellationToken.None);
                    }
                }
            }

            invocationCount = elapsedMilliseconds.Count;
            invocationTiming = ProfileInvocationTiming.Create(elapsedMilliseconds);
        }

        var afterProfile = host.CaptureSnapshot();
        var phaseSummary = options.Profile == ProfileKind.Trace
            ? PhaseTraceAnalyzer.Analyze(artifactPath)
            : [];
        IReadOnlyList<CacheMetricSummary> cacheSummary = [];
        if (options.Profile == ProfileKind.Trace && File.Exists(artifactPath))
        {
            cacheSummary = PhaseTraceAnalyzer.AnalyzeCacheMetrics(artifactPath);
        }

        var result = new ProfileRunResult
        {
            Repository = repository.Id,
            RepositorySize = repository.Size,
            Commit = repository.Commit,
            Scenario = string.Join(',', scenarios.Select(static scenario => scenario.Id)),
            Tool = string.Join(',', scenarios.Select(static scenario => scenario.Tool)),
            ScenarioSequence = scenarios.Select(static scenario => scenario.Id).ToArray(),
            ToolSequence = scenarios.Select(static scenario => scenario.Tool).ToArray(),
            Profile = options.Profile,
            StartedAtUtc = startedAtUtc,
            Environment = environment,
            RequestedDuration = options.Profile == ProfileKind.GcDump ? null : options.ProfileDuration,
            InvocationCount = invocationCount,
            WorkingSetBeforeBytes = beforeProfile.WorkingSetBytes,
            WorkingSetAfterBytes = afterProfile.WorkingSetBytes,
            WorkingSetDeltaBytes = afterProfile.WorkingSetBytes - beforeProfile.WorkingSetBytes,
            PeakWorkingSetBytes = afterProfile.PeakWorkingSetBytes,
            DiagnosticArtifact = artifactPath,
            PostCloseDiagnosticArtifact = options.Profile == ProfileKind.GcDump
                ? Path.Combine(outputDirectory, "heap-after-close.gcdump")
                : null,
            InvocationTiming = invocationTiming,
            PhaseSummary = phaseSummary,
            CacheSummary = cacheSummary,
        };

        await ResultWriter.WriteProfileAsync(outputDirectory, result, cancellationToken);
        if (phaseSummary.Count > 0)
        {
            await ResultWriter.WritePhaseSummaryAsync(
                outputDirectory,
                phaseSummary,
                invocationCount,
                invocationTiming,
                cancellationToken);
        }
    }

    private static async Task<Guid> OpenWorkspaceAsync(
        ScenarioHost host,
        string workspacePath,
        string repositoryRoot,
        CancellationToken cancellationToken,
        string alias = "performance")
    {
        var arguments = new Dictionary<string, object?>
        {
            ["alias"] = alias,
            ["path"] = workspacePath,
            ["workspaceRoot"] = repositoryRoot,
        };

        for (var attempt = 1; attempt <= _workspaceOpenMaximumAttempts; attempt++)
        {
            var result = await host.CallToolAsync(
                "workspace-open",
                arguments,
                cancellationToken);

            if (result.IsError == true)
            {
                var canRetry = attempt < _workspaceOpenMaximumAttempts
                    && IsWorkspaceChangedDuringLoadRetry(result);
                if (canRetry)
                {
                    Console.WriteLine(
                        $"workspace-open detected changing inputs; retrying after {_workspaceOpenRetryDelay.TotalMilliseconds:F0} ms ({attempt}/{_workspaceOpenMaximumAttempts - 1}).");

                    await Task.Delay(_workspaceOpenRetryDelay, cancellationToken);
                    continue;
                }

                throw new InvalidOperationException(
                    $"workspace-open failed: {result.StructuredContent?.GetRawText()}");
            }

            var structuredContent = result.StructuredContent
                ?? throw new InvalidDataException("workspace-open returned no structured content.");
            var workspace = structuredContent
                .GetProperty("data")
                .GetProperty("workspace");
            var workspaceId = workspace
                .GetProperty("workspaceId")
                .GetGuid();
            if (workspaceId == Guid.Empty)
            {
                throw new InvalidDataException("workspace-open returned an empty workspaceId.");
            }

            return workspaceId;
        }

        throw new InvalidOperationException(
            "workspace-open exhausted its bounded retry loop without returning a result.");
    }

    private static bool IsWorkspaceChangedDuringLoadRetry(
        ModelContextProtocol.Protocol.CallToolResult result)
    {
        if (result.StructuredContent is not { } content
            || !content.TryGetProperty("error", out var error)
            || !error.TryGetProperty("code", out var code))
        {
            return false;
        }

        var continuation = ToolContinuationReader.Read(content);
        var hasExpectedErrorCode = string.Equals(
            code.GetString(),
            "WorkspaceChangedDuringLoad",
            StringComparison.Ordinal);
        var hasExpectedContinuation = string.Equals(
            continuation?.Kind,
            "RetryRequest",
            StringComparison.Ordinal);

        return hasExpectedErrorCode && hasExpectedContinuation;
    }

    private static async Task CloseWorkspaceAsync(ScenarioHost host, Guid workspaceId)
    {
        var result = await host.CallToolAsync(
            "workspace-close",
            new Dictionary<string, object?>
            {
                ["workspace"] = new Dictionary<string, object?>
                {
                    ["workspaceId"] = workspaceId,
                },
            },
            CancellationToken.None);

        if (result.IsError == true)
        {
            throw new InvalidOperationException($"workspace-close failed: {result.StructuredContent?.GetRawText()}");
        }
    }

    private static ExceptionDispatchInfo CombineFailures(ExceptionDispatchInfo? current, Exception next)
    {
        return current is null
            ? ExceptionDispatchInfo.Capture(next)
            : ExceptionDispatchInfo.Capture(new AggregateException(current.SourceException, next));
    }

    private static RepositoryDefinition ResolveRepository(ScenarioSuite suite, string? repositoryId)
    {
        if (string.IsNullOrWhiteSpace(repositoryId))
        {
            throw new ArgumentException("--repository is required.");
        }

        return suite.Repositories.SingleOrDefault(
            repository => string.Equals(repository.Id, repositoryId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unknown repository '{repositoryId}'. Use 'list' to see available repositories.");
    }

    private static IReadOnlyList<ScenarioDefinition> ResolveScenarios(
        RepositoryDefinition repository,
        string? scenarioId,
        ScenarioCommand command)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("--scenario is required.");
        }

        if (string.Equals(scenarioId, _allScenarios, StringComparison.OrdinalIgnoreCase))
        {
            if (command is ScenarioCommand.Profile
                or ScenarioCommand.Commit
                or ScenarioCommand.CommitCancellation
                or ScenarioCommand.Conflict
                or ScenarioCommand.CrashRecovery
                or ScenarioCommand.StateSequence
                or ScenarioCommand.Concurrency)
            {
                throw new ArgumentException($"{command} requires one scenario; '--scenario all' is not supported.");
            }

            return repository.Scenarios
                .Where(static scenario => !scenario.CommitOnly)
                .ToArray();
        }

        var requestedScenarioIds = scenarioId.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (requestedScenarioIds.Length == 0)
        {
            throw new ArgumentException("--scenario must contain at least one scenario identifier.");
        }

        var scenarios = new List<ScenarioDefinition>(requestedScenarioIds.Length);
        foreach (var requestedScenarioId in requestedScenarioIds)
        {
            var scenario = repository.Scenarios.SingleOrDefault(
                item => string.Equals(item.Id, requestedScenarioId, StringComparison.OrdinalIgnoreCase));
            if (scenario is null)
            {
                throw new ArgumentException($"Unknown scenario '{requestedScenarioId}' for repository '{repository.Id}'.");
            }

            scenarios.Add(scenario);
        }

        if (command == ScenarioCommand.StateSequence
            && scenarios.Any(static scenario => scenario.StateSequence is null))
        {
            throw new ArgumentException(
                "The state-sequence command requires scenarios with a stateSequence definition.");
        }

        if (command == ScenarioCommand.Concurrency
            && scenarios.Any(static scenario => scenario.Concurrency is null))
        {
            throw new ArgumentException(
                "The concurrency command requires a scenario with a concurrency definition.");
        }

        return scenarios;
    }

    private static string ResolveCacheDirectory(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Path.GetFullPath(value);
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Path.GetTempPath(), "rwmcp", "r");
        }

        return Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp",
            "performance",
            "repositories");
    }

    private static string ResolveOutputDirectory(string? value, string frameworkRoot, string repositoryId)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Path.GetFullPath(value);
        }

        return Path.Combine(
            frameworkRoot,
            "artifacts",
            "performance",
            "results",
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{repositoryId}-{Guid.NewGuid():N}");
    }

    private static string CreateExecutionDirectory(string repositoryId)
    {
        string path;
        if (OperatingSystem.IsWindows())
        {
            path = Path.Combine(
                Path.GetTempPath(),
                "rwmcp",
                "x",
                $"{repositoryId}-{Guid.NewGuid():N}");
        }
        else
        {
            path = Path.Combine(
                Path.GetTempPath(),
                "roslyn-workbench-mcp",
                "performance",
                "execution",
                $"{repositoryId}-{Guid.NewGuid():N}");
        }

        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteExecutionDirectory(string? path)
    {
        if (path is null || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string ResolveFrameworkRoot(string? value)
    {
        var root = Path.GetFullPath(value ?? Environment.CurrentDirectory);
        if (!File.Exists(Path.Combine(root, ".config", "dotnet-tools.json")))
        {
            throw new DirectoryNotFoundException(
                $"'{root}' does not contain .config/dotnet-tools.json. Run from the Roslyn Workbench repository root or pass --framework-root.");
        }

        return root;
    }

    private static string ResolveRequiredPath(string? value, string option)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{option} is required.");
        }

        var path = Path.GetFullPath(value);
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException($"{option} path '{path}' does not exist.", path);
    }

    private static string GetArtifactName(ProfileKind profile)
    {
        return profile switch
        {
            ProfileKind.Trace => "profile.nettrace",
            ProfileKind.Counters => "counters.json",
            ProfileKind.GcDump => "heap.gcdump",
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown profile kind."),
        };
    }

    private static void ListSuite(ScenarioSuite suite)
    {
        foreach (var repository in suite.Repositories)
        {
            Console.WriteLine($"{repository.Id} ({repository.Size}) {repository.Url} @ {repository.Commit}");
            foreach (var scenario in repository.Scenarios)
            {
                Console.WriteLine($"  {scenario.Id}: {scenario.Tool} — {scenario.Description}");
            }
        }
    }

#pragma warning disable CA1303 // CLI help is intentionally invariant developer-facing text, not localised UI content.

    private static void WriteHelp()
    {
        Console.WriteLine(
            """
            Roslyn Workbench manual scenario runner

              list
              prepare --repository <id> [--cache <path>] [--framework-root <path>]
              measure --repository <id> --scenario <id|all> --host <path> [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]
              commit --repository <id> --scenario <mutation-id> --host <path> [--iterations 5] [--warmups 1] [--capture-trace] [--output <path>] [--framework-root <path>] [--skip-prepare]
              commit-cancellation --repository <id> --scenario <mutation-id> --host <path> [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]
              conflict --repository <id> --scenario <conflict-id> --host <path> [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]
              crash-recovery --repository <id> --scenario <mutation-id> --host <path> [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]
              state-sequence --repository <id> --scenario <state-sequence-id> --host <path> [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]
              concurrency --repository <id> --scenario <concurrency-id> --host <path> [--parallelism 4] [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]
              cancel --repository <id> --scenario <id> --host <path> [--cancel-after 00:00:00.050] [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]
              profile --repository <id> --scenario <id[,id...]> --host <path> [--plugin-directory <path>] [--profile trace|counters|gcdump] [--duration 00:00:30] [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]

            Repository clones default to the operating system's temporary directory. Results, state, and diagnostic captures default beneath artifacts/performance/results in the repository root.
            """);
    }

#pragma warning restore CA1303
}
