using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Diagnostics;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Cancellation;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CommitCancellation;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Concurrency;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Conflict;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CrashRecovery;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.StateSequences;
using Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Reporting;

internal static class ResultWriter
{
    private const string _transactionCommitOperation = "transaction-commit";
    private static readonly JsonSerializerOptions _serializerOptions = CreateSerializerOptions();

    public static async Task WriteMeasurementsAsync(
        string outputDirectory,
        IReadOnlyList<ScenarioRunResult> results,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "measurements.json");
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(stream, results, _serializerOptions, cancellationToken);
        }

        var markdown = CreateSummary(results);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "summary.md"),
            markdown,
            Encoding.UTF8,
            cancellationToken);
    }

    public static async Task WriteProfileAsync(
        string outputDirectory,
        ProfileRunResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "profile.json");
        await using var stream = File.Create(jsonPath);
        await JsonSerializer.SerializeAsync(stream, result, _serializerOptions, cancellationToken);
    }

    public static async Task WriteConcurrencyAsync(
        string outputDirectory,
        ConcurrencyRunResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "concurrency.json");
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                result,
                _serializerOptions,
                cancellationToken);
        }

        var batchTimings = new double[result.Batches.Count];
        var successfulInvocationTimings = new List<double>();
        var busyResponseTimings = new List<double>();
        var successfulThroughputs = new double[result.Batches.Count];
        var busyCount = 0;
        var successfulRetryCount = 0;
        for (var batchIndex = 0; batchIndex < result.Batches.Count; batchIndex++)
        {
            var batch = result.Batches[batchIndex];
            batchTimings[batchIndex] = batch.ElapsedMilliseconds;
            var successfulInvocationCount = 0;
            foreach (var invocation in batch.Invocations)
            {
                if (invocation.IsError)
                {
                    busyCount++;
                    busyResponseTimings.Add(invocation.ElapsedMilliseconds);
                }
                else
                {
                    successfulInvocationCount++;
                    successfulInvocationTimings.Add(invocation.ElapsedMilliseconds);
                }

                if (invocation.RetrySucceeded)
                {
                    successfulRetryCount++;
                }
            }

            successfulThroughputs[batchIndex] = batch.ElapsedMilliseconds <= 0
                ? 0
                : successfulInvocationCount / (batch.ElapsedMilliseconds / 1000);
        }

        Array.Sort(batchTimings);
        Array.Sort(successfulThroughputs);
        successfulInvocationTimings.Sort();
        busyResponseTimings.Sort();
        var successfulInvocationTimingArray = successfulInvocationTimings.ToArray();
        var busyResponseTimingArray = busyResponseTimings.ToArray();

        var builder = new StringBuilder()
            .AppendLine("# Roslyn Workbench concurrency summary")
            .AppendLine()
            .Append("Repository: ").AppendLine(result.Repository)
            .Append("Scenario: ").AppendLine(result.Scenario)
            .Append("Tool: ").AppendLine(result.Tool)
            .Append("Parallelism: ").AppendLine(
                result.Parallelism.ToString(CultureInfo.InvariantCulture))
            .Append("Measured batches: ").AppendLine(
                result.Batches.Count.ToString(CultureInfo.InvariantCulture))
            .Append("WorkspaceBusy responses: ").AppendLine(
                busyCount.ToString(CultureInfo.InvariantCulture))
            .Append("Successful retries: ").AppendLine(
                successfulRetryCount.ToString(CultureInfo.InvariantCulture))
            .Append("Median batch: ").Append(
                Percentile(batchTimings, 0.5).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms")
            .Append("P95 batch: ").Append(
                Percentile(batchTimings, 0.95).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms")
            .Append("Median successful query: ").Append(
                Percentile(
                    successfulInvocationTimingArray,
                    0.5).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms")
            .Append("P95 successful query: ").Append(
                Percentile(
                    successfulInvocationTimingArray,
                    0.95).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms")
            .Append("Median WorkspaceBusy response: ").Append(
                Percentile(
                    busyResponseTimingArray,
                    0.5).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms")
            .Append("Median successful throughput: ").Append(
                Percentile(
                    successfulThroughputs,
                    0.5).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" queries/s")
            .AppendLine()
            .AppendLine("## Multi-Workspace validation")
            .AppendLine()
            .Append("Listed Workspaces: ").AppendLine(
                result.MultiWorkspace.ListedWorkspaceCount.ToString(CultureInfo.InvariantCulture))
            .Append("Parallel cross-Workspace query pair: ").Append(
                result.MultiWorkspace.ParallelQueryElapsedMilliseconds.ToString(
                    "F2",
                    CultureInfo.InvariantCulture)).AppendLine(" ms")
            .AppendLine()
            .AppendLine("| Step | Tool | Elapsed (ms) | Error | Code | Required action |")
            .AppendLine("|---|---|---:|---|---|---|");

        foreach (var step in result.MultiWorkspace.Steps)
        {
            builder
                .Append("| ").Append(step.Name)
                .Append(" | ").Append(step.Tool)
                .Append(" | ").Append(
                    step.ElapsedMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(step.IsError ? "Yes" : "No")
                .Append(" | ").Append(step.ErrorCode ?? string.Empty)
                .Append(" | ").Append(step.RequiredAction ?? string.Empty)
                .AppendLine(" |");
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "concurrency.md"),
            builder.ToString(),
            Encoding.UTF8,
            cancellationToken);
    }

    public static async Task WriteDurableCommitAsync(
        string outputDirectory,
        DurableCommitRunResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "commit.json");
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(stream, result, _serializerOptions, cancellationToken);
        }

        var validations = result.Measurements
            .Select(static measurement => measurement.Validation)
            .ToArray();

        var validationPath = Path.Combine(outputDirectory, "validation.json");
        await using (var stream = File.Create(validationPath))
        {
            await JsonSerializer.SerializeAsync(stream, validations, _serializerOptions, cancellationToken);
        }

        var commitTimings = result.Measurements
            .Select(static item => item.CommitMilliseconds)
            .Order()
            .ToArray();
        var stagingTimings = result.Measurements
            .Select(static item => item.StagingMilliseconds)
            .Order()
            .ToArray();
        var previewTimings = result.Measurements
            .Select(static item => item.PreviewMilliseconds)
            .Order()
            .ToArray();
        var restorationTimings = result.Measurements
            .Select(static item => item.RestorationMilliseconds)
            .Order()
            .ToArray();
        var peakWorkingSetIncreases = result.Measurements
            .Select(static item => item.CommitMemory.PeakWorkingSetIncreaseBytes)
            .Order()
            .ToArray();
        var peakPrivateMemoryIncreases = result.Measurements
            .Select(static item => item.CommitMemory.PeakPrivateMemoryIncreaseBytes)
            .Order()
            .ToArray();
        var sampledPeakWorkingSets = result.Measurements
            .Select(static item => item.CommitMemory.PeakWorkingSetBytes)
            .Order()
            .ToArray();
        var sampledPeakPrivateMemory = result.Measurements
            .Select(static item => item.CommitMemory.PeakPrivateMemoryBytes)
            .Order()
            .ToArray();
        var first = result.Measurements[0];
        var stableFileSet = HasStableFileSet(result.Measurements);
        var builder = new StringBuilder()
            .AppendLine("# Roslyn Workbench durable commit summary")
            .AppendLine()
            .Append("Repository: ").AppendLine(result.Repository)
            .Append("Scenario: ").AppendLine(result.Scenario)
            .Append("Mutation tool: ").AppendLine(result.MutationTool)
            .Append("Warm-ups: ").AppendLine(result.WarmupCount.ToString(CultureInfo.InvariantCulture))
            .Append("Measured iterations: ").AppendLine(result.Measurements.Count.ToString(CultureInfo.InvariantCulture))
            .Append("Changed files: ").AppendLine(first.ChangedFileCount.ToString(CultureInfo.InvariantCulture))
            .Append("Created/replaced/deleted: ")
            .Append(first.CreatedFileCount.ToString(CultureInfo.InvariantCulture)).Append('/')
            .Append(first.ReplacedFileCount.ToString(CultureInfo.InvariantCulture)).Append('/')
            .AppendLine(first.DeletedFileCount.ToString(CultureInfo.InvariantCulture))
            .Append("Original bytes: ").AppendLine(first.OriginalBytes.ToString(CultureInfo.InvariantCulture))
            .Append("Committed bytes: ").AppendLine(first.CommittedBytes.ToString(CultureInfo.InvariantCulture))
            .Append("Stable changed-file set: ").AppendLine(stableFileSet ? "Yes" : "No")
            .AppendLine()
            .AppendLine("| Phase | Median (ms) | P95 (ms) |")
            .AppendLine("|---|---:|---:|")
            .Append("| Mutation staging | ")
            .Append(Percentile(stagingTimings, 0.5).ToString("F2", CultureInfo.InvariantCulture)).Append(" | ")
            .Append(Percentile(stagingTimings, 0.95).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" |")
            .Append("| Transaction preview | ")
            .Append(Percentile(previewTimings, 0.5).ToString("F2", CultureInfo.InvariantCulture)).Append(" | ")
            .Append(Percentile(previewTimings, 0.95).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" |")
            .Append("| Durable commit | ")
            .Append(Percentile(commitTimings, 0.5).ToString("F2", CultureInfo.InvariantCulture)).Append(" | ")
            .Append(Percentile(commitTimings, 0.95).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" |")
            .Append("| Repository restoration | ")
            .Append(Percentile(restorationTimings, 0.5).ToString("F2", CultureInfo.InvariantCulture)).Append(" | ")
            .Append(Percentile(restorationTimings, 0.95).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" |");

        builder
            .AppendLine()
            .AppendLine("## Host memory during durable commit")
            .AppendLine()
            .Append("Sampling interval: ")
            .Append(first.CommitMemory.SamplingIntervalMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" ms")
            .AppendLine()
            .AppendLine("The sampled peak is scoped to `transaction-commit`; the increase is relative to the post-staging, post-preview baseline immediately before that call.")
            .AppendLine()
            .AppendLine("| Metric | Median | P95 |")
            .AppendLine("|---|---:|---:|")
            .Append("| Sampled peak working set | ")
            .Append(FormatBytes(Percentile(sampledPeakWorkingSets, 0.5))).Append(" | ")
            .Append(FormatBytes(Percentile(sampledPeakWorkingSets, 0.95))).AppendLine(" |")
            .Append("| Peak working-set increase | ")
            .Append(FormatBytes(Percentile(peakWorkingSetIncreases, 0.5))).Append(" | ")
            .Append(FormatBytes(Percentile(peakWorkingSetIncreases, 0.95))).AppendLine(" |")
            .Append("| Sampled peak private memory | ")
            .Append(FormatBytes(Percentile(sampledPeakPrivateMemory, 0.5))).Append(" | ")
            .Append(FormatBytes(Percentile(sampledPeakPrivateMemory, 0.95))).AppendLine(" |")
            .Append("| Peak private-memory increase | ")
            .Append(FormatBytes(Percentile(peakPrivateMemoryIncreases, 0.5))).Append(" | ")
            .Append(FormatBytes(Percentile(peakPrivateMemoryIncreases, 0.95))).AppendLine(" |");

        AppendDurableCommitPhases(builder, first.PhaseSummary);
        AppendAtomicFileCommitRetries(builder, result.Measurements);

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "commit.md"),
            builder.ToString(),
            Encoding.UTF8,
            cancellationToken);
    }

    private static bool HasStableFileSet(IReadOnlyList<DurableCommitMeasurement> measurements)
    {
        var expected = measurements[0].Files;
        for (var measurementIndex = 1; measurementIndex < measurements.Count; measurementIndex++)
        {
            var actual = measurements[measurementIndex].Files;
            if (actual.Count != expected.Count)
            {
                return false;
            }

            for (var fileIndex = 0; fileIndex < expected.Count; fileIndex++)
            {
                if (expected[fileIndex].Operation != actual[fileIndex].Operation
                    || !string.Equals(
                        expected[fileIndex].Path,
                        actual[fileIndex].Path,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void AppendDurableCommitPhases(
        StringBuilder builder,
        IReadOnlyList<PhaseTraceSummary> phases)
    {
        var headingWritten = false;
        foreach (var phase in phases)
        {
            if (!string.Equals(
                phase.Operation,
                _transactionCommitOperation,
                StringComparison.Ordinal))
            {
                continue;
            }

            if (!headingWritten)
            {
                builder
                    .AppendLine()
                    .AppendLine("## Host commit phases")
                    .AppendLine()
                    .AppendLine("Nested phases overlap their parent and must not be added together.")
                    .AppendLine()
                    .AppendLine("| Phase | Count | Median (ms) | P95 (ms) | Total (ms) |")
                    .AppendLine("|---|---:|---:|---:|---:|");
                headingWritten = true;
            }

            builder
                .Append("| ").Append(phase.Phase)
                .Append(" | ").Append(phase.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(phase.MedianMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(phase.P95Milliseconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(phase.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
                .AppendLine(" |");
        }
    }

    private static void AppendAtomicFileCommitRetries(
        StringBuilder builder,
        IReadOnlyList<DurableCommitMeasurement> measurements)
    {
        var summaries = measurements
            .Select(static measurement => measurement.AtomicFileCommitRetries)
            .OfType<AtomicFileCommitRetrySummary>()
            .ToArray();

        if (summaries.Length == 0)
        {
            return;
        }

        builder
            .AppendLine()
            .AppendLine("## Atomic file replacement retries")
            .AppendLine()
            .AppendLine("Retry metrics are captured only while commit tracing is enabled. They contain counts and planned backoff time, not filesystem paths.")
            .AppendLine()
            .Append("Measured commits: ").AppendLine(summaries.Length.ToString(CultureInfo.InvariantCulture))
            .Append("Total retry attempts: ").AppendLine(summaries.Sum(static item => item.TotalRetryAttempts).ToString(CultureInfo.InvariantCulture))
            .Append("Atomic operations requiring retry: ").AppendLine(summaries.Sum(static item => item.RetriedOperationCount).ToString(CultureInfo.InvariantCulture))
            .Append("Highest retry count for one operation: ").AppendLine(summaries.Max(static item => item.MaximumRetriesForOneOperation).ToString(CultureInfo.InvariantCulture))
            .Append("Total planned backoff: ").Append(summaries.Sum(static item => item.TotalDelayMilliseconds).ToString(CultureInfo.InvariantCulture)).AppendLine(" ms");
    }

    public static async Task WriteCancellationAsync(
        string outputDirectory,
        CancellationRunResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "cancellation.json");
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(stream, result, _serializerOptions, cancellationToken);
        }

        var clientLatency = result.Measurements
            .Select(static item => item.ClientCancellationLatencyMilliseconds)
            .Order()
            .ToArray();
        var recoveryLatency = result.Measurements
            .Select(static item => item.ExclusiveLeaseRecoveryMilliseconds)
            .Order()
            .ToArray();
        var canceledCount = result.Measurements.Count(static item => item.OperationCanceled);
        var completedCount = result.Measurements.Count(static item => item.CompletedBeforeCancellation);
        var builder = new StringBuilder();
        builder.AppendLine("# Roslyn Workbench cancellation summary");
        builder.AppendLine();
        builder.Append("Repository: ").AppendLine(result.Repository);
        builder.Append("Scenario: ").AppendLine(result.Scenario);
        builder.Append("Cancellation delay: ").Append(result.CancellationDelay.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms");
        builder.Append("Cancelled invocations: ").Append(canceledCount.ToString(CultureInfo.InvariantCulture)).Append('/').AppendLine(result.Measurements.Count.ToString(CultureInfo.InvariantCulture));
        builder.Append("Completed before cancellation: ").AppendLine(completedCount.ToString(CultureInfo.InvariantCulture));
        builder.Append("Median client cancellation latency: ").Append(Percentile(clientLatency, 0.5).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms");
        builder.Append("P95 client cancellation latency: ").Append(Percentile(clientLatency, 0.95).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms");
        builder.Append("Median exclusive-lease recovery: ").Append(Percentile(recoveryLatency, 0.5).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms");
        builder.Append("P95 exclusive-lease recovery: ").Append(Percentile(recoveryLatency, 0.95).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms");

        var markdown = builder.ToString();

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "cancellation.md"),
            markdown,
            Encoding.UTF8,
            cancellationToken);
    }

    public static async Task WriteCommitCancellationAsync(
        string outputDirectory,
        CommitCancellationRunResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(
            outputDirectory,
            "commit-cancellation.json");
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                result,
                _serializerOptions,
                cancellationToken);
        }

        var validations = result.Measurements
            .Select(static measurement => measurement.Validation)
            .ToArray();
        var validationPath = Path.Combine(outputDirectory, "validation.json");
        await using (var stream = File.Create(validationPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                validations,
                _serializerOptions,
                cancellationToken);
        }

        var builder = new StringBuilder()
            .AppendLine("# Roslyn Workbench commit cancellation summary")
            .AppendLine()
            .Append("Repository: ").AppendLine(result.Repository)
            .Append("Scenario: ").AppendLine(result.Scenario)
            .Append("Mutation tool: ").AppendLine(result.MutationTool)
            .Append("Warm-ups: ").AppendLine(
                result.WarmupCount.ToString(CultureInfo.InvariantCulture))
            .Append("Measured boundary executions: ").AppendLine(
                result.Measurements.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine()
            .AppendLine("| Boundary | Observed phase | Notification (ms) | Client completion (ms) | Settlement (ms) | Cancelled | Committed | Changed files |")
            .AppendLine("|---|---|---:|---:|---:|---|---|---:|");

        foreach (var measurement in result.Measurements)
        {
            builder
                .Append("| ").Append(measurement.Boundary)
                .Append(" | ").Append(measurement.ObservedPhase)
                .Append(" | ").Append(
                    measurement.CancellationNotificationMilliseconds.ToString(
                        "F2",
                        CultureInfo.InvariantCulture))
                .Append(" | ").Append(
                    measurement.CompletionAfterCancellationMilliseconds.ToString(
                        "F2",
                        CultureInfo.InvariantCulture))
                .Append(" | ").Append(
                    measurement.SettlementMilliseconds.ToString(
                        "F2",
                        CultureInfo.InvariantCulture))
                .Append(" | ").Append(
                    measurement.OperationCanceled ? "Yes" : "No")
                .Append(" | ").Append(
                    measurement.Committed ? "Yes" : "No")
                .Append(" | ").Append(
                    measurement.Files.Count.ToString(
                        CultureInfo.InvariantCulture))
                .AppendLine(" |");
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "commit-cancellation.md"),
            builder.ToString(),
            Encoding.UTF8,
            cancellationToken);
    }

    public static async Task WriteConflictAsync(
        string outputDirectory,
        ConflictRunResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "conflict.json");
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                result,
                _serializerOptions,
                cancellationToken);
        }

        var validations = result.Measurements
            .Select(static measurement => measurement.Validation)
            .ToArray();
        var validationPath = Path.Combine(outputDirectory, "validation.json");
        await using (var stream = File.Create(validationPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                validations,
                _serializerOptions,
                cancellationToken);
        }

        var commit = result.Measurements
            .Select(static measurement => measurement.CommitMilliseconds)
            .Order()
            .ToArray();
        var detection = result.Measurements
            .Select(static measurement => measurement.ConflictDetectionMilliseconds)
            .Order()
            .ToArray();
        var recovery = result.Measurements
            .Select(static measurement => measurement.RecoveryMilliseconds)
            .Order()
            .ToArray();
        var first = result.Measurements[0];
        var builder = new StringBuilder()
            .AppendLine("# Roslyn Workbench controlled conflict summary")
            .AppendLine()
            .Append("Repository: ").AppendLine(result.Repository)
            .Append("Scenario: ").AppendLine(result.Scenario)
            .Append("Mode: ").AppendLine(result.Mode.ToString())
            .Append("Warm-ups: ").AppendLine(
                result.WarmupCount.ToString(CultureInfo.InvariantCulture))
            .Append("Measured iterations: ").AppendLine(
                result.Measurements.Count.ToString(CultureInfo.InvariantCulture))
            .Append("Error/next: ").Append(first.ErrorCode).Append('/')
            .AppendLine(first.RequiredAction ?? "None")
            .Append("Files left changed before runner restoration: ").AppendLine(
                first.FilesBeforeRestoration.Count.ToString(CultureInfo.InvariantCulture))
            .Append("Recovery state/artifacts: ")
            .Append(first.RecoveryState ?? "None").Append('/')
            .AppendLine(first.RecoveryArtifactCount.ToString(CultureInfo.InvariantCulture))
            .AppendLine()
            .AppendLine("| Measurement | Median (ms) | P95 (ms) |")
            .AppendLine("|---|---:|---:|")
            .Append("| Commit result | ")
            .Append(Percentile(commit, 0.5).ToString("F2", CultureInfo.InvariantCulture))
            .Append(" | ")
            .Append(Percentile(commit, 0.95).ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" |")
            .Append("| Conflict detection/injection | ")
            .Append(Percentile(detection, 0.5).ToString("F2", CultureInfo.InvariantCulture))
            .Append(" | ")
            .Append(Percentile(detection, 0.95).ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" |")
            .Append("| Recovery after injection | ")
            .Append(Percentile(recovery, 0.5).ToString("F2", CultureInfo.InvariantCulture))
            .Append(" | ")
            .Append(Percentile(recovery, 0.95).ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" |");

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "conflict.md"),
            builder.ToString(),
            Encoding.UTF8,
            cancellationToken);
    }

    public static async Task WriteCrashRecoveryAsync(
        string outputDirectory,
        CrashRecoveryRunResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "crash-recovery.json");
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                result,
                _serializerOptions,
                cancellationToken);
        }

        var validations = result.Measurements
            .Select(static measurement => measurement.Validation)
            .ToArray();
        var validationPath = Path.Combine(outputDirectory, "validation.json");
        await using (var stream = File.Create(validationPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                validations,
                _serializerOptions,
                cancellationToken);
        }

        var interruption = result.Measurements
            .Select(static measurement => measurement.InterruptionMilliseconds)
            .Order()
            .ToArray();

        var startupRecovery = result.Measurements
            .Select(static measurement => measurement.RecoveryStartupMilliseconds)
            .Order()
            .ToArray();

        var workspaceReopen = result.Measurements
            .Select(static measurement => measurement.WorkspaceReopenMilliseconds)
            .Order()
            .ToArray();

        var first = result.Measurements[0];
        var builder = new StringBuilder()
            .AppendLine("# Roslyn Workbench crash recovery summary")
            .AppendLine()
            .Append("Repository: ").AppendLine(result.Repository)
            .Append("Scenario: ").AppendLine(result.Scenario)
            .Append("Mutation tool: ").AppendLine(result.MutationTool)
            .Append("Warm-ups: ").AppendLine(
                result.WarmupCount.ToString(CultureInfo.InvariantCulture))
            .Append("Measured iterations: ").AppendLine(
                result.Measurements.Count.ToString(CultureInfo.InvariantCulture))
            .Append("Prepared recovery state/artifacts: ")
            .Append(first.PreparedRecoveryState ?? "None").Append('/')
            .AppendLine(first.PreparedRecoveryArtifactCount.ToString(CultureInfo.InvariantCulture))
            .Append("Files partially applied before termination: ")
            .AppendLine(first.FilesBeforeRecovery.Count.ToString(CultureInfo.InvariantCulture))
            .Append("Observed applied target: ").AppendLine(first.AppliedTargetPath)
            .AppendLine()
            .AppendLine("| Measurement | Median (ms) | P95 (ms) |")
            .AppendLine("|---|---:|---:|")
            .Append("| Commit start to forced termination | ")
            .Append(Percentile(interruption, 0.5).ToString("F2", CultureInfo.InvariantCulture))
            .Append(" | ")
            .Append(Percentile(interruption, 0.95).ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" |")
            .Append("| Fresh Host startup and recovery | ")
            .Append(Percentile(startupRecovery, 0.5).ToString("F2", CultureInfo.InvariantCulture))
            .Append(" | ")
            .Append(Percentile(startupRecovery, 0.95).ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" |")
            .Append("| Workspace reopen after recovery | ")
            .Append(Percentile(workspaceReopen, 0.5).ToString("F2", CultureInfo.InvariantCulture))
            .Append(" | ")
            .Append(Percentile(workspaceReopen, 0.95).ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" |");

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "crash-recovery.md"),
            builder.ToString(),
            Encoding.UTF8,
            cancellationToken);
    }

    public static async Task WriteStateSequenceAsync(
        string outputDirectory,
        StateSequenceRunResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "state-sequence.json");
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                result,
                _serializerOptions,
                cancellationToken);
        }

        var validations = result.Measurements
            .Select(static measurement => measurement.Validation)
            .ToArray();
        var validationPath = Path.Combine(outputDirectory, "validation.json");
        await using (var stream = File.Create(validationPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                validations,
                _serializerOptions,
                cancellationToken);
        }

        var first = result.Measurements[0];
        var createdCount = first.Files.Count(
            static file => file.Operation == DurableCommitFileOperation.Create);
        var replacedCount = first.Files.Count(
            static file => file.Operation == DurableCommitFileOperation.Replace);
        var deletedCount = first.Files.Count(
            static file => file.Operation == DurableCommitFileOperation.Delete);
        var builder = new StringBuilder()
            .AppendLine("# Roslyn Workbench state-sequence summary")
            .AppendLine()
            .Append("Repository: ").AppendLine(result.Repository)
            .Append("Scenario: ").AppendLine(result.Scenario)
            .Append("Sequence kind: ").AppendLine(result.Kind.ToString())
            .Append("Warm-ups: ").AppendLine(
                result.WarmupCount.ToString(CultureInfo.InvariantCulture))
            .Append("Measured iterations: ").AppendLine(
                result.Measurements.Count.ToString(CultureInfo.InvariantCulture))
            .Append("Created/replaced/deleted before restoration: ")
            .Append(createdCount.ToString(CultureInfo.InvariantCulture)).Append('/')
            .Append(replacedCount.ToString(CultureInfo.InvariantCulture)).Append('/')
            .AppendLine(deletedCount.ToString(CultureInfo.InvariantCulture))
            .AppendLine();

        if (first.ExternalCommand is not null)
        {
            builder
                .Append("External command: ").Append(first.ExternalCommand.FileName)
                .Append(' ').AppendLine(string.Join(' ', first.ExternalCommand.Arguments))
                .Append("External command elapsed: ")
                .Append(first.ExternalCommand.ElapsedMilliseconds.ToString(
                    "F2",
                    CultureInfo.InvariantCulture))
                .AppendLine(" ms")
                .Append("Host CPU during build/settlement: ")
                .Append(first.ExternalCommand.HostCpuMilliseconds.ToString(
                    "F2",
                    CultureInfo.InvariantCulture))
                .AppendLine(" ms")
                .Append("Host working set before/after/delta: ")
                .Append(FormatBytes(first.ExternalCommand.HostWorkingSetBeforeBytes))
                .Append('/')
                .Append(FormatBytes(first.ExternalCommand.HostWorkingSetAfterBytes))
                .Append('/')
                .AppendLine(FormatBytes(first.ExternalCommand.HostWorkingSetDeltaBytes))
                .Append("Host peak working set: ")
                .AppendLine(FormatBytes(first.ExternalCommand.HostPeakWorkingSetBytes))
                .Append("External stdout/stderr bytes: ")
                .Append(first.ExternalCommand.StandardOutputBytes.ToString(
                    CultureInfo.InvariantCulture))
                .Append('/')
                .AppendLine(first.ExternalCommand.StandardErrorBytes.ToString(
                    CultureInfo.InvariantCulture))
                .AppendLine();
        }

        if (first.WatcherStress is not null)
        {
            builder
                .Append("Watcher stress artifact path: ")
                .AppendLine(first.WatcherStress.ArtifactPath)
                .Append("Watcher stress files/write passes: ")
                .Append(first.WatcherStress.FileCount.ToString(
                    CultureInfo.InvariantCulture))
                .Append('/')
                .AppendLine(first.WatcherStress.WritePasses.ToString(
                    CultureInfo.InvariantCulture))
                .Append("Baseline/stressed reload: ")
                .Append(first.WatcherStress.BaselineReloadMilliseconds.ToString(
                    "F2",
                    CultureInfo.InvariantCulture))
                .Append('/')
                .Append(first.WatcherStress.StressedReloadMilliseconds.ToString(
                    "F2",
                    CultureInfo.InvariantCulture))
                .AppendLine(" ms")
                .Append("Stressed reload delta: ")
                .Append(first.WatcherStress.ReloadDeltaMilliseconds.ToString(
                    "F2",
                    CultureInfo.InvariantCulture))
                .AppendLine(" ms")
                .Append("Watcher stress and concurrent reload elapsed: ")
                .Append(first.WatcherStress.ElapsedMilliseconds.ToString(
                    "F2",
                    CultureInfo.InvariantCulture))
                .AppendLine(" ms")
                .Append("Host CPU during stress/reload/settlement: ")
                .Append(first.WatcherStress.HostCpuMilliseconds.ToString(
                    "F2",
                    CultureInfo.InvariantCulture))
                .AppendLine(" ms")
                .Append("Host working set before/after/delta: ")
                .Append(FormatBytes(first.WatcherStress.HostWorkingSetBeforeBytes))
                .Append('/')
                .Append(FormatBytes(first.WatcherStress.HostWorkingSetAfterBytes))
                .Append('/')
                .AppendLine(FormatBytes(first.WatcherStress.HostWorkingSetDeltaBytes))
                .Append("Host peak working set: ")
                .AppendLine(FormatBytes(first.WatcherStress.HostPeakWorkingSetBytes))
                .AppendLine();
        }

        builder
            .AppendLine("| Step | Tool | Elapsed (ms) | Error | Workspace state | Change source | Change kind | Change error | Change path | References | Revision | Revisions |")
            .AppendLine("|---|---|---:|---|---|---|---|---|---|---:|---:|---:|");

        foreach (var step in first.Steps)
        {
            var externalChange = step.ExternalChange;
            builder
                .Append("| ").Append(step.Name)
                .Append(" | ").Append(step.Tool)
                .Append(" | ").Append(
                    step.ElapsedMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(step.ErrorCode ?? string.Empty)
                .Append(" | ").Append(step.WorkspaceState ?? string.Empty)
                .Append(" | ").Append(externalChange?.DetectionSource ?? string.Empty)
                .Append(" | ").Append(externalChange?.Kind ?? string.Empty)
                .Append(" | ").Append(externalChange?.ErrorCode ?? string.Empty)
                .Append(" | ").Append(EscapeMarkdownCell(externalChange?.Path))
                .Append(" | ").Append(FormatNullable(step.ReferenceCount))
                .Append(" | ").Append(FormatNullable(step.TransactionRevision))
                .Append(" | ").Append(FormatNullable(step.TransactionRevisionCount))
                .AppendLine(" |");
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "state-sequence.md"),
            builder.ToString(),
            Encoding.UTF8,
            cancellationToken);
    }

    private static string FormatNullable(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string EscapeMarkdownCell(string? value)
    {
        return value?.Replace("|", "\\|", StringComparison.Ordinal) ?? string.Empty;
    }

    private static string FormatBytes(long value)
    {
        return $"{value.ToString(CultureInfo.InvariantCulture)} bytes";
    }

    public static async Task WritePhaseSummaryAsync(
        string outputDirectory,
        IReadOnlyList<PhaseTraceSummary> phases,
        int invocationCount,
        ProfileInvocationTiming? invocationTiming,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder()
            .AppendLine("# Roslyn Workbench phase summary")
            .AppendLine()
            .Append("Profiled invocations: ").AppendLine(invocationCount.ToString(CultureInfo.InvariantCulture))
            .AppendLine()
            .AppendLine("Phase timings are Host-internal elapsed durations captured only while the custom EventSource provider is enabled. Nested phases overlap their parent and must not be added together.")
            .AppendLine();

        AppendInvocationReconciliation(builder, phases, invocationTiming);

        builder
            .AppendLine("| Operation | Phase | Count | Median (ms) | P95 (ms) | Total (ms) | Median share of tool total |")
            .AppendLine("|---|---|---:|---:|---:|---:|---:|");

        foreach (var phase in phases)
        {
            builder
                .Append("| ").Append(phase.Operation)
                .Append(" | ").Append(phase.Phase)
                .Append(" | ").Append(phase.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(phase.MedianMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(phase.P95Milliseconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(phase.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(phase.MedianToolSharePercent.ToString("F1", CultureInfo.InvariantCulture))
                .AppendLine("% |");
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "phases.md"),
            builder.ToString(),
            Encoding.UTF8,
            cancellationToken);
    }

    public static async Task WriteRunValidationAsync(
        string outputDirectory,
        RunValidationResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "validation.json");
        await using var stream = File.Create(jsonPath);
        await JsonSerializer.SerializeAsync(stream, result, _serializerOptions, cancellationToken);
    }

    private static void AppendInvocationReconciliation(
        StringBuilder builder,
        IReadOnlyList<PhaseTraceSummary> phases,
        ProfileInvocationTiming? invocationTiming)
    {
        if (invocationTiming is null)
        {
            return;
        }

        var toolTotals = phases
            .Where(static phase => phase.Phase == "tool-total")
            .ToArray();
        if (toolTotals.Length != 1)
        {
            return;
        }

        var toolTotal = toolTotals[0];
        var uninstrumentedMedian = Math.Max(
            0,
            invocationTiming.MedianMilliseconds - toolTotal.MedianMilliseconds);

        builder
            .AppendLine("| Boundary | Median (ms) | P95 (ms) |")
            .AppendLine("|---|---:|---:|")
            .Append("| End-to-end MCP invocation | ")
            .Append(invocationTiming.MedianMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
            .Append(" | ")
            .Append(invocationTiming.P95Milliseconds.ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" |")
            .Append("| Instrumented Host tool | ")
            .Append(toolTotal.MedianMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
            .Append(" | ")
            .Append(toolTotal.P95Milliseconds.ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" |")
            .Append("| Outside instrumented Host tool | ")
            .Append(uninstrumentedMedian.ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" | — |")
            .AppendLine();
    }

    private static string CreateSummary(IReadOnlyList<ScenarioRunResult> results)
    {
        var builder = new StringBuilder()
            .AppendLine("# Roslyn Workbench performance summary")
            .AppendLine()
            .AppendLine("| Repository | Size | Scenario | Tool | Warm-ups | First measured (ms) | Subsequent median (ms) | Median elapsed (ms) | P95 elapsed (ms) | Median host CPU (ms) | Max working set (MiB) | Response (KiB) | Max Code Action reference (bytes) | Exact response stable |")
            .AppendLine("|---|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");

        foreach (var result in results)
        {
            var elapsed = result.Measurements.Select(static item => item.ElapsedMilliseconds).Order().ToArray();
            var cpu = result.Measurements.Select(static item => item.HostCpuMilliseconds).Order().ToArray();
            var maxWorkingSet = result.Measurements.Max(static item => item.WorkingSetBytes);
            var responseBytes = result.Measurements.Max(static item => item.ResponseBytes);
            var maximumCodeActionReferenceBytes = result.Measurements
                .Max(static item => item.CodeActionReferences?.MaximumBytes ?? 0);

            var firstMeasured = result.Measurements[0].ElapsedMilliseconds;
            var subsequent = result.Measurements
                .Skip(1)
                .Select(static item => item.ElapsedMilliseconds)
                .Order()
                .ToArray();
            var stableResponse = result.Measurements
                .Select(static item => item.ResponseSha256)
                .Distinct(StringComparer.Ordinal)
                .Count() == 1;

            builder
                .Append("| ").Append(result.Repository)
                .Append(" | ").Append(result.RepositorySize)
                .Append(" | ").Append(result.Scenario)
                .Append(" | ").Append(result.Tool)
                .Append(" | ").Append(result.WarmupCount.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(firstMeasured.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(Percentile(subsequent, 0.5).ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(Percentile(elapsed, 0.5).ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(Percentile(elapsed, 0.95).ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(Percentile(cpu, 0.5).ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append((maxWorkingSet / 1024d / 1024d).ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append((responseBytes / 1024d).ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(maximumCodeActionReferenceBytes.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(stableResponse ? "Yes" : "No")
                .AppendLine(" |");
        }

        return builder.ToString();
    }

    private static double Percentile(double[] orderedValues, double percentile)
    {
        if (orderedValues.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * orderedValues.Length) - 1;
        return orderedValues[Math.Max(0, index)];
    }

    private static long Percentile(long[] orderedValues, double percentile)
    {
        if (orderedValues.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * orderedValues.Length) - 1;
        return orderedValues[Math.Max(0, index)];
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
