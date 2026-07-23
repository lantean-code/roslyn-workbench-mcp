using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Performance;

internal static class PerformanceApplication
{
    private const string _allScenarios = "all";

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The manual runner must preserve workload, workspace-close, and Host-disposal failures so cleanup is attempted and every failure is reported together.")]
    public static async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string? executionDirectory = null;
        try
        {
            var options = PerformanceOptions.Parse(arguments);
            if (options.Command == PerformanceCommand.Help)
            {
                WriteHelp();
                return 0;
            }

            var suite = await PerformanceSuiteLoader.LoadAsync(cancellationToken);
            if (options.Command == PerformanceCommand.List)
            {
                ListSuite(suite);
                return 0;
            }

            var repository = ResolveRepository(suite, options.Repository);
            var frameworkRoot = ResolveFrameworkRoot(options.FrameworkRoot);
            var cacheDirectory = ResolveCacheDirectory(options.CacheDirectory);
            var manager = new RepositoryManager(cacheDirectory);
            var repositoryRoot = await manager.PrepareAsync(
                repository,
                !options.SkipPreparation,
                cancellationToken);

            if (options.Command == PerformanceCommand.Prepare)
            {
                Console.WriteLine($"Prepared {repository.Id} at {repositoryRoot}");
                return 0;
            }

            var hostPath = ResolveRequiredPath(options.HostPath, "--host");
            var outputDirectory = ResolveOutputDirectory(options.OutputDirectory, frameworkRoot, repository.Id);
            executionDirectory = CreateExecutionDirectory(repository.Id);
            var scenarios = ResolveScenarios(repository, options.Scenario, options.Command);
            var environment = RunEnvironmentInfo.Capture(hostPath);

            if (options.Command == PerformanceCommand.Commit)
            {
                await MeasureDurableCommitsAsync(
                    options,
                    repository,
                    scenarios,
                    hostPath,
                    repositoryRoot,
                    environment,
                    frameworkRoot,
                    executionDirectory,
                    outputDirectory,
                    cancellationToken);

                await Console.Out.WriteLineAsync($"Results: {outputDirectory}");
                return 0;
            }

            if (options.Command == PerformanceCommand.Conflict)
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

            var stateDirectory = Path.Combine(executionDirectory, "state");
            var workspacePath = Path.Combine(repositoryRoot, repository.WorkspacePath);
            var initialWorkspaceStateFiles = RunStateValidator.CaptureWorkspaceStateFiles(repositoryRoot);

            await using var host = await PerformanceHost.StartAsync(
                hostPath,
                repositoryRoot,
                stateDirectory,
                cancellationToken);
            string? workspaceId = null;
            ExceptionDispatchInfo? runFailure = null;
            var workspaceClosed = false;

            try
            {
                workspaceId = await OpenWorkspaceAsync(host, workspacePath, repositoryRoot, cancellationToken);
                var runner = new ScenarioRunner(host, workspaceId, repositoryRoot);

                if (options.Command == PerformanceCommand.Measure)
                {
                    await MeasureAsync(options, repository, scenarios, runner, environment, outputDirectory, cancellationToken);
                }
                else if (options.Command == PerformanceCommand.Cancel)
                {
                    await MeasureCancellationAsync(options, repository, scenarios, runner, environment, outputDirectory, cancellationToken);
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
                if (workspaceId is not null)
                {
                    try
                    {
                        await CloseWorkspaceAsync(host, workspaceId);
                        workspaceClosed = true;
                    }
                    catch (Exception exception)
                    {
                        runFailure = CombineFailures(runFailure, exception);
                    }
                }

                if (workspaceClosed
                    && options.Command == PerformanceCommand.Profile
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
        PerformanceOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        ScenarioRunner runner,
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
        PerformanceOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        ScenarioRunner runner,
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
    }

    private static async Task MeasureDurableCommitsAsync(
        PerformanceOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        string hostPath,
        string repositoryRoot,
        RunEnvironmentInfo environment,
        string frameworkRoot,
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
        DiagnosticCollector? collector = null;
        if (options.CaptureTrace)
        {
            await DiagnosticCollector.EnsureToolsRestoredAsync(frameworkRoot, cancellationToken);
            collector = new DiagnosticCollector(frameworkRoot);
        }

        var startedAtUtc = DateTimeOffset.UtcNow;

        for (var warmup = 1; warmup <= options.Warmups; warmup++)
        {
            await Console.Out.WriteLineAsync(
                $"Warming durable commit {repository.Id}/{scenario.Id} ({warmup}/{options.Warmups})");
            _ = await RunDurableCommitIterationAsync(
                repository,
                scenario,
                hostPath,
                repositoryRoot,
                workspacePath,
                Path.Combine(executionDirectory, "state", $"warmup-{warmup}"),
                initialWorkspaceStateFiles,
                restorer,
                iteration: 0,
                collector: null,
                diagnosticArtifact: null,
                options.ProfileDuration,
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
                collector,
                collector is null
                    ? null
                    : Path.Combine(outputDirectory, $"commit-iteration-{iteration}.nettrace"),
                options.ProfileDuration,
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
        DiagnosticCollector? collector,
        string? diagnosticArtifact,
        TimeSpan profileDuration,
        CancellationToken cancellationToken)
    {
        await using var host = await PerformanceHost.StartAsync(
            hostPath,
            repositoryRoot,
            stateDirectory,
            cancellationToken);
        string? workspaceId = null;
        DurableCommitRunner? runner = null;
        DurableCommitExecution? execution = null;
        ExceptionDispatchInfo? runFailure = null;

        try
        {
            workspaceId = await OpenWorkspaceAsync(host, workspacePath, repositoryRoot, cancellationToken);
            runner = new DurableCommitRunner(host, workspaceId, repositoryRoot);
            if (collector is null || diagnosticArtifact is null)
            {
                execution = await runner.ExecuteAsync(scenario, cancellationToken);
            }
            else
            {
                var preparation = await runner.PrepareAsync(scenario, cancellationToken);
                Directory.CreateDirectory(Path.GetDirectoryName(diagnosticArtifact)!);
                using var diagnosticProcess = collector.StartDurationProfile(
                    ProfileKind.Trace,
                    host.ProcessId,
                    profileDuration,
                    diagnosticArtifact);
                var standardOutput = diagnosticProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                var standardError = diagnosticProcess.StandardError.ReadToEndAsync(cancellationToken);

                try
                {
                    await DiagnosticCollector.WaitForCollectionStartAsync(
                        diagnosticProcess,
                        cancellationToken);

                    execution = await runner.CommitAsync(preparation, cancellationToken);
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

            if (workspaceId is not null)
            {
                try
                {
                    await CloseWorkspaceAsync(host, workspaceId);
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
        };
    }

    private static async Task MeasureConflictsAsync(
        PerformanceOptions options,
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
            _ = await RunConflictIterationAsync(
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
        await using var host = await PerformanceHost.StartAsync(
            hostPath,
            repositoryRoot,
            stateDirectory,
            cancellationToken);
        string? workspaceId = null;
        ConflictExecution? execution = null;
        ExceptionDispatchInfo? runFailure = null;

        try
        {
            workspaceId = await OpenWorkspaceAsync(
                host,
                workspacePath,
                repositoryRoot,
                cancellationToken);
            var durableRunner = new DurableCommitRunner(
                host,
                workspaceId,
                repositoryRoot);
            var preparation = await durableRunner.PrepareAsync(
                scenario,
                cancellationToken);
            var conflictRunner = new ConflictRunner(
                host,
                workspaceId,
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
            if (workspaceId is not null
                && conflict.Mode == ConflictMode.PreWriteDrift)
            {
                try
                {
                    await CloseWorkspaceAsync(host, workspaceId);
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
            RequiredAction = completedExecution.RequiredAction,
            ExternalMutation = completedExecution.ExternalMutation,
            FilesBeforeRestoration = completedChanges.Files,
            RecoveryState = completedRecoveryEvidence.State,
            RecoveryArtifactCount = completedRecoveryEvidence.ArtifactCount,
            HostShutdown = shutdown,
            Validation = validation,
        };
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
        PerformanceOptions options,
        RepositoryDefinition repository,
        IReadOnlyList<ScenarioDefinition> scenarios,
        PerformanceHost host,
        ScenarioRunner runner,
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
            if (scenarios.Count != 1)
            {
                throw new ArgumentException("Trace and counter profiling require exactly one scenario.");
            }

            var scenario = scenarios[0];
            await runner.WarmUpAsync(scenario, options.Warmups, cancellationToken);
            using var diagnosticProcess = collector.StartDurationProfile(
                options.Profile,
                host.ProcessId,
                options.ProfileDuration,
                artifactPath);
            try
            {
                var standardOutput = diagnosticProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                var standardError = diagnosticProcess.StandardError.ReadToEndAsync(cancellationToken);
                var elapsedMilliseconds = await runner.RunUntilExitAsync(
                    scenario,
                    diagnosticProcess,
                    cancellationToken);

                invocationCount = elapsedMilliseconds.Count;
                invocationTiming = ProfileInvocationTiming.Create(elapsedMilliseconds);
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

        var phaseSummary = options.Profile == ProfileKind.Trace
            ? PhaseTraceAnalyzer.Analyze(artifactPath)
            : [];

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
            DiagnosticArtifact = artifactPath,
            PostCloseDiagnosticArtifact = options.Profile == ProfileKind.GcDump
                ? Path.Combine(outputDirectory, "heap-after-close.gcdump")
                : null,
            InvocationTiming = invocationTiming,
            PhaseSummary = phaseSummary,
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

    private static async Task<string> OpenWorkspaceAsync(
        PerformanceHost host,
        string workspacePath,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var result = await host.CallToolAsync(
            "workspace-open",
            new Dictionary<string, object?>
            {
                ["alias"] = "performance",
                ["path"] = workspacePath,
                ["workspaceRoot"] = repositoryRoot,
            },
            cancellationToken);

        if (result.IsError == true)
        {
            throw new InvalidOperationException($"workspace-open failed: {result.StructuredContent?.GetRawText()}");
        }

        var structuredContent = result.StructuredContent
            ?? throw new InvalidDataException("workspace-open returned no structured content.");

        return structuredContent
            .GetProperty("data")
            .GetProperty("workspace")
            .GetProperty("workspaceId")
            .GetString()
            ?? throw new InvalidDataException("workspace-open returned no workspaceId.");
    }

    private static async Task CloseWorkspaceAsync(PerformanceHost host, string workspaceId)
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

    private static RepositoryDefinition ResolveRepository(PerformanceSuite suite, string? repositoryId)
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
        PerformanceCommand command)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("--scenario is required.");
        }

        if (string.Equals(scenarioId, _allScenarios, StringComparison.OrdinalIgnoreCase))
        {
            if (command is PerformanceCommand.Profile
                or PerformanceCommand.Commit
                or PerformanceCommand.Conflict)
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

        return scenarios;
    }

    private static string ResolveCacheDirectory(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Path.GetFullPath(value);
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
        var path = Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp",
            "performance",
            "execution",
            $"{repositoryId}-{Guid.NewGuid():N}");
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

    private static void ListSuite(PerformanceSuite suite)
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
            Roslyn Workbench manual performance runner

              list
              prepare --repository <id> [--cache <path>] [--framework-root <path>]
              measure --repository <id> --scenario <id|all> --host <path> [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]
              commit --repository <id> --scenario <mutation-id> --host <path> [--iterations 5] [--warmups 1] [--capture-trace] [--duration 00:00:30] [--output <path>] [--framework-root <path>] [--skip-prepare]
              conflict --repository <id> --scenario <conflict-id> --host <path> [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]
              cancel --repository <id> --scenario <id> --host <path> [--cancel-after 00:00:00.050] [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]
              profile --repository <id> --scenario <id[,id...]> --host <path> [--profile trace|counters|gcdump] [--duration 00:00:30] [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]

            Repository clones default to the operating system's temporary directory. Results, state, and diagnostic captures default beneath artifacts/performance/results in the repository root.
            """);
    }
#pragma warning restore CA1303
}
