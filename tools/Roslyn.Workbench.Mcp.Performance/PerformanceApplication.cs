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
            var scenarios = ResolveScenarios(repository, options.Scenario, options.Command);
            var environment = RunEnvironmentInfo.Capture(hostPath);
            var stateDirectory = Path.Combine(outputDirectory, "state");
            var workspacePath = Path.Combine(repositoryRoot, repository.WorkspacePath);
            var initialWorkspaceStateFiles = RunStateValidator.CaptureWorkspaceStateFiles(repositoryRoot);

            await using var host = await PerformanceHost.StartAsync(
                hostPath,
                repositoryRoot,
                stateDirectory,
                cancellationToken);
            string? workspaceId = null;
            ExceptionDispatchInfo? runFailure = null;

            try
            {
                workspaceId = await OpenWorkspaceAsync(host, workspacePath, repositoryRoot, cancellationToken);
                var runner = new ScenarioRunner(host, workspaceId, repositoryRoot);

                if (options.Command == PerformanceCommand.Measure)
                {
                    await MeasureAsync(options, repository, scenarios, runner, environment, outputDirectory, cancellationToken);
                }
                else
                {
                    await ProfileAsync(options, repository, scenarios[0], host, runner, environment, frameworkRoot, outputDirectory, cancellationToken);
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
                Measurements = measurements,
            });
        }

        await ResultWriter.WriteMeasurementsAsync(outputDirectory, results, cancellationToken);
    }

    private static async Task ProfileAsync(
        PerformanceOptions options,
        RepositoryDefinition repository,
        ScenarioDefinition scenario,
        PerformanceHost host,
        ScenarioRunner runner,
        RunEnvironmentInfo environment,
        string frameworkRoot,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        await DiagnosticCollector.EnsureToolsRestoredAsync(frameworkRoot, cancellationToken);
        await runner.WarmUpAsync(scenario, options.Warmups, cancellationToken);
        Directory.CreateDirectory(outputDirectory);
        var collector = new DiagnosticCollector(frameworkRoot);
        var artifactPath = Path.Combine(outputDirectory, GetArtifactName(options.Profile));
        var startedAtUtc = DateTimeOffset.UtcNow;
        int invocationCount;

        if (options.Profile == ProfileKind.GcDump)
        {
            await runner.RunCountAsync(scenario, options.Iterations, cancellationToken);
            invocationCount = options.Iterations;
            await collector.CollectGcDumpAsync(host.ProcessId, artifactPath, cancellationToken);
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
                invocationCount = await runner.RunUntilExitAsync(scenario, diagnosticProcess, cancellationToken);
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

        var result = new ProfileRunResult
        {
            Repository = repository.Id,
            RepositorySize = repository.Size,
            Commit = repository.Commit,
            Scenario = scenario.Id,
            Tool = scenario.Tool,
            Profile = options.Profile,
            StartedAtUtc = startedAtUtc,
            Environment = environment,
            RequestedDuration = options.Profile == ProfileKind.GcDump ? null : options.ProfileDuration,
            InvocationCount = invocationCount,
            DiagnosticArtifact = artifactPath,
        };

        await ResultWriter.WriteProfileAsync(outputDirectory, result, cancellationToken);
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
            if (command == PerformanceCommand.Profile)
            {
                throw new ArgumentException("Profiling requires one scenario; '--scenario all' is not supported.");
            }

            return repository.Scenarios;
        }

        var scenario = repository.Scenarios.SingleOrDefault(
            item => string.Equals(item.Id, scenarioId, StringComparison.OrdinalIgnoreCase));

        return scenario is null
            ? throw new ArgumentException($"Unknown scenario '{scenarioId}' for repository '{repository.Id}'.")
            : [scenario];
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
              profile --repository <id> --scenario <id> --host <path> [--profile trace|counters|gcdump] [--duration 00:00:30] [--iterations 5] [--warmups 1] [--output <path>] [--framework-root <path>] [--skip-prepare]

            Repository clones default to the operating system's temporary directory. Results, state, and diagnostic captures default beneath artifacts/performance/results in the repository root.
            """);
    }
#pragma warning restore CA1303
}
