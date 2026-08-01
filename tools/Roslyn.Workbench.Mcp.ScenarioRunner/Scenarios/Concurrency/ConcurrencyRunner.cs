using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Concurrency;

internal sealed class ConcurrencyRunner
{
    private readonly ScenarioHost _host;
    private readonly Guid _primaryWorkspaceId;
    private readonly string _repositoryRoot;
    private readonly Guid _secondaryWorkspaceId;
    private Guid? _transactionWorkspaceId;

    public ConcurrencyRunner(
        ScenarioHost host,
        Guid primaryWorkspaceId,
        Guid secondaryWorkspaceId,
        string repositoryRoot)
    {
        _host = host;
        _primaryWorkspaceId = primaryWorkspaceId;
        _secondaryWorkspaceId = secondaryWorkspaceId;
        _repositoryRoot = repositoryRoot;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A failed manual concurrency scenario must attempt transaction rollback before preserving and rethrowing the original failure.")]
    public async Task<ConcurrencyExecution> ExecuteAsync(
        ScenarioDefinition scenario,
        int warmupCount,
        int iterationCount,
        int parallelism,
        CancellationToken cancellationToken)
    {
        var definition = ValidateScenario(scenario);

        var primaryArguments = Materialize(scenario, _primaryWorkspaceId);
        var secondaryArguments = ArgumentMaterializer.Materialize(
            definition.SecondaryArguments,
            _secondaryWorkspaceId,
            _repositoryRoot,
            _host.GetWorkspaceEpoch(_secondaryWorkspaceId));
        for (var warmup = 0; warmup < warmupCount; warmup++)
        {
            await InvokeRequiredAsync(
                "primary-warmup",
                scenario.Tool,
                primaryArguments,
                cancellationToken);
        }

        var primaryBaseline = await InvokeRequiredAsync(
            "primary-baseline",
            scenario.Tool,
            primaryArguments,
            cancellationToken);

        var batches = new List<ConcurrentBatchMeasurement>();
        for (var iteration = 1; iteration <= iterationCount; iteration++)
        {
            var batch = await MeasureBatchAsync(
                scenario,
                primaryArguments,
                primaryBaseline.ResponseSha256,
                definition.ValidateSingleFlight,
                GetExpectedFactoryExecutionCount(
                    primaryBaseline,
                    iteration,
                    definition.ValidateSingleFlight),
                iteration,
                parallelism,
                cancellationToken);

            batches.Add(batch);
        }

        _transactionWorkspaceId = null;
        try
        {
            var multiWorkspace = await MeasureMultiWorkspaceAsync(
                scenario,
                primaryArguments,
                secondaryArguments,
                primaryBaseline,
                definition.ValidateSingleFlight,
                cancellationToken);

            return new ConcurrencyExecution
            {
                Batches = batches,
                MultiWorkspace = multiWorkspace,
            };
        }
        catch
        {
            if (_transactionWorkspaceId is Guid transactionWorkspaceId)
            {
                await TryRollbackAsync(transactionWorkspaceId);
            }

            throw;
        }
    }

    private async Task<ConcurrentBatchMeasurement> MeasureBatchAsync(
        ScenarioDefinition scenario,
        IReadOnlyDictionary<string, object?> arguments,
        string expectedSha256,
        bool validateSingleFlight,
        int? expectedFactoryExecutionCount,
        int iteration,
        int parallelism,
        CancellationToken cancellationToken)
    {
        var startGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var invocations = new Task<ConcurrentInvocationMeasurement>[parallelism];
        for (var slot = 0; slot < parallelism; slot++)
        {
            invocations[slot] = InvokeAfterStartAsync(
                startGate.Task,
                scenario,
                arguments,
                expectedSha256,
                validateSingleFlight,
                slot + 1,
                cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        startGate.SetResult();
        var measurements = await Task.WhenAll(invocations);
        stopwatch.Stop();

        for (var index = 0; index < measurements.Length; index++)
        {
            var measurement = measurements[index];
            if (!measurement.IsError)
            {
                continue;
            }

            if (validateSingleFlight)
            {
                throw new InvalidOperationException(
                    "The single-flight calibration batch could not execute every concurrent request.");
            }

            var retry = await InvokeRequiredAsync(
                $"parallel-read-{measurement.Slot}-retry",
                scenario.Tool,
                arguments,
                cancellationToken);

            ValidateResponse(
                retry,
                expectedSha256,
                validateSingleFlight,
                $"Retry for parallel read {measurement.Slot} did not match the primary baseline.");

            measurements[index] = measurement with { RetrySucceeded = true };
        }

        if (validateSingleFlight)
        {
            ValidateSingleFlightBatch(measurements, expectedFactoryExecutionCount);
        }

        return new ConcurrentBatchMeasurement
        {
            Iteration = iteration,
            ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            Invocations = measurements,
        };
    }

    private async Task<ConcurrentInvocationMeasurement> InvokeAfterStartAsync(
        Task start,
        ScenarioDefinition scenario,
        IReadOnlyDictionary<string, object?> arguments,
        string expectedSha256,
        bool validateSingleFlight,
        int slot,
        CancellationToken cancellationToken)
    {
        await start;

        var measurement = await InvokeAsync(
            $"parallel-read-{slot}",
            scenario.Tool,
            arguments,
            cancellationToken);

        if (measurement.IsError)
        {
            ValidateWorkspaceBusy(measurement);
        }
        else
        {
            ValidateResponse(
                measurement,
                expectedSha256,
                validateSingleFlight,
                $"Parallel read {slot} did not match the primary baseline.");
        }

        return new ConcurrentInvocationMeasurement
        {
            Slot = slot,
            ElapsedMilliseconds = measurement.ElapsedMilliseconds,
            ResponseBytes = measurement.ResponseBytes,
            ResponseSha256 = measurement.ResponseSha256,
            IsError = measurement.IsError,
            ErrorCode = measurement.ErrorCode,
            RequiredAction = measurement.RequiredAction,
            RetrySucceeded = false,
            FactoryExecutionCount = measurement.FactoryExecutionCount,
        };
    }

    private async Task<MultiWorkspaceMeasurement> MeasureMultiWorkspaceAsync(
        ScenarioDefinition scenario,
        IReadOnlyDictionary<string, object?> primaryArguments,
        IReadOnlyDictionary<string, object?> secondaryArguments,
        ConcurrencyStepMeasurement primaryBaseline,
        bool validateSingleFlight,
        CancellationToken cancellationToken)
    {
        var steps = new List<ConcurrencyStepMeasurement> { primaryBaseline };
        var secondaryBaseline = await InvokeRequiredAsync(
            "secondary-baseline",
            scenario.Tool,
            secondaryArguments,
            cancellationToken);

        steps.Add(secondaryBaseline);

        var workspaceList = await InvokeRequiredAsync(
            "workspace-list",
            "workspace-list",
            new Dictionary<string, object?>(),
            cancellationToken);

        steps.Add(workspaceList);
        var workspaceCount = GetWorkspaceCount(workspaceList);
        if (workspaceCount != 2)
        {
            throw new InvalidOperationException(
                $"The Host listed {workspaceCount} Workspaces; the scenario expected exactly two.");
        }

        var primaryStart = await InvokeRequiredAsync(
            "primary-transaction-start",
            "transaction-start",
            CreateWorkspaceArguments(_primaryWorkspaceId),
            cancellationToken);

        steps.Add(primaryStart);
        _transactionWorkspaceId = _primaryWorkspaceId;

        var secondaryDuringPrimary = await InvokeRequiredAsync(
            "secondary-query-during-primary-transaction",
            scenario.Tool,
            secondaryArguments,
            cancellationToken);

        steps.Add(secondaryDuringPrimary);
        ValidateResponse(
            secondaryDuringPrimary,
            secondaryBaseline.ResponseSha256,
            validateSingleFlight,
            "The secondary Workspace query changed while the primary Workspace owned the transaction.");

        var secondaryRejected = await InvokeAsync(
            "secondary-transaction-rejected",
            "transaction-start",
            CreateWorkspaceArguments(_secondaryWorkspaceId),
            cancellationToken);

        steps.Add(secondaryRejected);
        ValidateTransactionOwnerRejection(secondaryRejected);

        var primaryRollback = await InvokeRequiredAsync(
            "primary-transaction-rollback",
            "transaction-rollback",
            CreateWorkspaceArguments(_primaryWorkspaceId),
            cancellationToken);

        steps.Add(primaryRollback);
        _transactionWorkspaceId = null;

        var secondaryStart = await InvokeRequiredAsync(
            "secondary-transaction-start",
            "transaction-start",
            CreateWorkspaceArguments(_secondaryWorkspaceId),
            cancellationToken);

        steps.Add(secondaryStart);
        _transactionWorkspaceId = _secondaryWorkspaceId;

        var primaryDuringSecondary = await InvokeRequiredAsync(
            "primary-query-during-secondary-transaction",
            scenario.Tool,
            primaryArguments,
            cancellationToken);

        steps.Add(primaryDuringSecondary);
        ValidateResponse(
            primaryDuringSecondary,
            primaryBaseline.ResponseSha256,
            validateSingleFlight,
            "The primary Workspace query changed while the secondary Workspace owned the transaction.");

        var secondaryRollback = await InvokeRequiredAsync(
            "secondary-transaction-rollback",
            "transaction-rollback",
            CreateWorkspaceArguments(_secondaryWorkspaceId),
            cancellationToken);

        steps.Add(secondaryRollback);
        _transactionWorkspaceId = null;

        var startGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var primaryTask = InvokeAfterStartAsync(
            startGate.Task,
            scenario,
            primaryArguments,
            primaryBaseline.ResponseSha256,
            validateSingleFlight,
            slot: 1,
            cancellationToken: cancellationToken);
        var secondaryTask = InvokeAfterStartAsync(
            startGate.Task,
            scenario,
            secondaryArguments,
            secondaryBaseline.ResponseSha256,
            validateSingleFlight,
            slot: 2,
            cancellationToken: cancellationToken);

        var parallelStopwatch = Stopwatch.StartNew();
        startGate.SetResult();
        await Task.WhenAll(primaryTask, secondaryTask);
        parallelStopwatch.Stop();

        return new MultiWorkspaceMeasurement
        {
            PrimaryWorkspaceId = _primaryWorkspaceId,
            SecondaryWorkspaceId = _secondaryWorkspaceId,
            ListedWorkspaceCount = workspaceCount,
            ParallelQueryElapsedMilliseconds = parallelStopwatch.Elapsed.TotalMilliseconds,
            Steps = steps,
        };
    }

    private async Task<ConcurrencyStepMeasurement> InvokeRequiredAsync(
        string name,
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var measurement = await InvokeAsync(name, tool, arguments, cancellationToken);
        if (measurement.IsError)
        {
            throw new InvalidOperationException(
                $"Tool '{tool}' returned an MCP error during '{name}': {measurement.ErrorCode} / {measurement.RequiredAction}.");
        }

        return measurement;
    }

    private async Task<ConcurrencyStepMeasurement> InvokeAsync(
        string name,
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await _host.CallToolAsync(tool, arguments, cancellationToken);
        stopwatch.Stop();

        var observation = ResponseObservation.Create(result);
        return new ConcurrencyStepMeasurement
        {
            Name = name,
            Tool = tool,
            ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            IsError = result.IsError == true,
            ResponseBytes = observation.Bytes,
            ResponseSha256 = observation.Sha256,
            ErrorCode = GetStructuredString(result, "error", "code"),
            RequiredAction = GetStructuredString(result, "next"),
            WorkspaceCount = GetWorkspaceCount(result),
            Workload = GetStructuredString(result, "data", "workload"),
            FactoryExecutionCount = GetStructuredInteger(
                result,
                "data",
                "factoryExecutionCount"),
            PayloadLength = GetStructuredInteger(result, "data", "payloadLength"),
        };
    }

    private IReadOnlyDictionary<string, object?> Materialize(
        ScenarioDefinition scenario,
        Guid workspaceId)
    {
        return ArgumentMaterializer.Materialize(
            scenario.Arguments,
            workspaceId,
            _repositoryRoot,
            _host.GetWorkspaceEpoch(workspaceId));
    }

    private static Dictionary<string, object?> CreateWorkspaceArguments(
        Guid workspaceId)
    {
        return new Dictionary<string, object?>
        {
            ["workspace"] = new Dictionary<string, object?>
            {
                ["workspaceId"] = workspaceId,
            },
        };
    }

    private static int GetWorkspaceCount(ConcurrencyStepMeasurement measurement)
    {
        return measurement.WorkspaceCount
            ?? throw new InvalidDataException(
                "workspace-list returned no Workspace collection.");
    }

    private static int? GetWorkspaceCount(CallToolResult result)
    {
        if (result.StructuredContent is not JsonElement content
            || !content.TryGetProperty("data", out var data)
            || !data.TryGetProperty("workspaces", out var workspaces)
            || workspaces.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return workspaces.GetArrayLength();
    }

    private static string? GetStructuredString(CallToolResult result, params string[] path)
    {
        if (result.StructuredContent is not JsonElement element)
        {
            return null;
        }

        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static int? GetStructuredInteger(CallToolResult result, params string[] path)
    {
        if (result.StructuredContent is not JsonElement element)
        {
            return null;
        }

        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out var value)
                ? value
                : null;
    }

    private async Task TryRollbackAsync(Guid workspaceId)
    {
        await _host.CallToolAsync(
            "transaction-rollback",
            CreateWorkspaceArguments(workspaceId),
            CancellationToken.None);
    }

    private static ConcurrencyDefinition ValidateScenario(ScenarioDefinition scenario)
    {
        var definition = scenario.Concurrency
            ?? throw new InvalidDataException(
                $"Scenario '{scenario.Id}' does not define concurrency settings.");

        if (scenario.Setup.Count > 0 || scenario.Cleanup.Count > 0 || scenario.CommitOnly)
        {
            throw new InvalidDataException(
                $"Concurrency scenario '{scenario.Id}' must be a query without setup, cleanup or commit-only behaviour.");
        }

        return definition;
    }

    private static void ValidateResponse(
        ConcurrencyStepMeasurement actual,
        string expectedSha256,
        bool validateSingleFlight,
        string message)
    {
        if (validateSingleFlight)
        {
            if (actual.Workload is null
                || actual.PayloadLength != 0
                || actual.FactoryExecutionCount is null)
            {
                throw new InvalidOperationException(message);
            }

            return;
        }

        if (!string.Equals(actual.ResponseSha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static int? GetExpectedFactoryExecutionCount(
        ConcurrencyStepMeasurement baseline,
        int iteration,
        bool validateSingleFlight)
    {
        if (!validateSingleFlight)
        {
            return null;
        }

        var baselineCount = baseline.FactoryExecutionCount
            ?? throw new InvalidOperationException(
                "The single-flight calibration baseline reported no factory execution count.");

        return checked(baselineCount + iteration);
    }

    private static void ValidateSingleFlightBatch(
        ConcurrentInvocationMeasurement[] measurements,
        int? expectedFactoryExecutionCount)
    {
        if (expectedFactoryExecutionCount is null)
        {
            throw new InvalidOperationException(
                "The single-flight calibration batch has no expected factory execution count.");
        }

        if (measurements.Length == 0
            || measurements.Any(static measurement => measurement.IsError))
        {
            throw new InvalidOperationException(
                "The single-flight calibration batch did not complete every concurrent request.");
        }

        var actualCounts = measurements
            .Select(static measurement => measurement.FactoryExecutionCount)
            .ToArray();

        if (actualCounts.Any(count => count != expectedFactoryExecutionCount))
        {
            throw new InvalidOperationException(
                $"The single-flight calibration expected factory execution count {expectedFactoryExecutionCount.Value}, but observed {string.Join(", ", actualCounts.Select(static count => count?.ToString(CultureInfo.InvariantCulture) ?? "missing"))}.");
        }
    }

    private static void ValidateTransactionOwnerRejection(
        ConcurrencyStepMeasurement measurement)
    {
        if (!measurement.IsError
            || !string.Equals(
                measurement.ErrorCode,
                "TransactionOwnedByWorkspace",
                StringComparison.Ordinal)
            || !string.Equals(
                measurement.RequiredAction,
                "CommitOrRollback",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Starting a transaction for the non-owner Workspace did not reject with TransactionOwnedByWorkspace and CommitOrRollback.");
        }
    }

    private static void ValidateWorkspaceBusy(ConcurrencyStepMeasurement measurement)
    {
        if (!string.Equals(
                measurement.ErrorCode,
                "WorkspaceBusy",
                StringComparison.Ordinal)
            || !string.Equals(
                measurement.RequiredAction,
                "Retry",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "An excess concurrent read did not reject with WorkspaceBusy and Retry.");
        }
    }
}
