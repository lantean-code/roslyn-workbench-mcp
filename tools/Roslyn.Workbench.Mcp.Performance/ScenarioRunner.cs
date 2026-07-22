using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.Performance;

internal sealed class ScenarioRunner
{
    private static readonly TimeSpan _exclusiveLeaseRecoveryTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _exclusiveLeaseRetryDelay = TimeSpan.FromMilliseconds(10);
    private readonly PerformanceHost _host;
    private readonly string _workspaceId;
    private readonly string _repositoryRoot;

    public ScenarioRunner(PerformanceHost host, string workspaceId, string repositoryRoot)
    {
        _host = host;
        _workspaceId = workspaceId;
        _repositoryRoot = repositoryRoot;
    }

    public async Task WarmUpAsync(
        ScenarioDefinition scenario,
        int count,
        CancellationToken cancellationToken)
    {
        for (var iteration = 0; iteration < count; iteration++)
        {
            await InvokeScenarioAsync(scenario, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<InvocationMeasurement>> MeasureAsync(
        ScenarioDefinition scenario,
        int count,
        CancellationToken cancellationToken)
    {
        var measurements = new List<InvocationMeasurement>();
        for (var iteration = 1; iteration <= count; iteration++)
        {
            await InvokeSupportingCallsAsync(scenario.Setup, cancellationToken);
            CallToolResult result;
            HostSnapshot after;
            var before = _host.CaptureSnapshot();
            var stopwatch = new Stopwatch();

            try
            {
                stopwatch.Start();
                result = await InvokeCoreAsync(scenario, cancellationToken);
                stopwatch.Stop();
                after = _host.CaptureSnapshot();
            }
            finally
            {
                await InvokeSupportingCallsAsync(scenario.Cleanup, cancellationToken);
            }

            var observation = ResponseObservation.Create(result);

            measurements.Add(new InvocationMeasurement
            {
                Iteration = iteration,
                ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                HostCpuMilliseconds = (after.CpuTime - before.CpuTime).TotalMilliseconds,
                WorkingSetBytes = after.WorkingSetBytes,
                WorkingSetDeltaBytes = after.WorkingSetBytes - before.WorkingSetBytes,
                PeakWorkingSetBytes = after.PeakWorkingSetBytes,
                ResponseBytes = observation.Bytes,
                ResponseSha256 = observation.Sha256,
                BoundedCollections = observation.BoundedCollections,
                MutationStaged = observation.MutationStaged,
                IsError = result.IsError == true,
            });
        }

        return measurements;
    }

    public async Task<IReadOnlyList<double>> RunUntilExitAsync(
        ScenarioDefinition scenario,
        Process diagnosticProcess,
        CancellationToken cancellationToken)
    {
        var elapsedMilliseconds = new List<double>();
        while (!diagnosticProcess.HasExited)
        {
            await InvokeSupportingCallsAsync(scenario.Setup, cancellationToken);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await InvokeCoreAsync(scenario, cancellationToken);
                stopwatch.Stop();
                elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            finally
            {
                await InvokeSupportingCallsAsync(scenario.Cleanup, cancellationToken);
            }
        }

        return elapsedMilliseconds;
    }

    public async Task RunCountAsync(
        ScenarioDefinition scenario,
        int count,
        CancellationToken cancellationToken)
    {
        for (var iteration = 0; iteration < count; iteration++)
        {
            await InvokeScenarioAsync(scenario, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<CancellationMeasurement>> MeasureCancellationAsync(
        ScenarioDefinition scenario,
        int count,
        TimeSpan cancellationDelay,
        CancellationToken cancellationToken)
    {
        if (scenario.Setup.Count > 0 || scenario.Cleanup.Count > 0)
        {
            throw new InvalidOperationException("Cancellation measurements require a query scenario without setup or cleanup calls.");
        }

        var measurements = new List<CancellationMeasurement>();
        for (var iteration = 1; iteration <= count; iteration++)
        {
            var arguments = ArgumentMaterializer.Materialize(scenario.Arguments, _workspaceId, _repositoryRoot);
            var (requestId, invocation) = _host.StartCancellableToolCall(scenario.Tool, arguments, cancellationToken);
            var cancellationDelayTask = Task.Delay(cancellationDelay, cancellationToken);
            var completedTask = await Task.WhenAny(invocation, cancellationDelayTask);
            if (completedTask == invocation)
            {
                var result = await invocation;
                ThrowIfScenarioFailed(scenario.Id, result);

                measurements.Add(new CancellationMeasurement
                {
                    Iteration = iteration,
                    CancellationRequestedAfterMilliseconds = cancellationDelay.TotalMilliseconds,
                    ClientCancellationLatencyMilliseconds = 0,
                    ExclusiveLeaseRecoveryMilliseconds = 0,
                    CompletedBeforeCancellation = true,
                    OperationCanceled = false,
                });

                continue;
            }

            var cancellationStopwatch = Stopwatch.StartNew();
            await _host.CancelToolCallAsync(requestId, cancellationToken);
            var operationCanceled = false;
            try
            {
                await invocation;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                operationCanceled = true;
            }

            cancellationStopwatch.Stop();

            var recoveryStopwatch = Stopwatch.StartNew();
            await VerifyExclusiveLeaseRecoveryAsync(cancellationToken);
            recoveryStopwatch.Stop();

            measurements.Add(new CancellationMeasurement
            {
                Iteration = iteration,
                CancellationRequestedAfterMilliseconds = cancellationDelay.TotalMilliseconds,
                ClientCancellationLatencyMilliseconds = cancellationStopwatch.Elapsed.TotalMilliseconds,
                ExclusiveLeaseRecoveryMilliseconds = recoveryStopwatch.Elapsed.TotalMilliseconds,
                CompletedBeforeCancellation = false,
                OperationCanceled = operationCanceled,
            });
        }

        return measurements;
    }

    private async Task<CallToolResult> InvokeScenarioAsync(
        ScenarioDefinition scenario,
        CancellationToken cancellationToken)
    {
        await InvokeSupportingCallsAsync(scenario.Setup, cancellationToken);

        try
        {
            return await InvokeCoreAsync(scenario, cancellationToken);
        }
        finally
        {
            await InvokeSupportingCallsAsync(scenario.Cleanup, cancellationToken);
        }
    }

    private async Task<CallToolResult> InvokeCoreAsync(
        ScenarioDefinition scenario,
        CancellationToken cancellationToken)
    {
        var arguments = ArgumentMaterializer.Materialize(scenario.Arguments, _workspaceId, _repositoryRoot);
        var result = await _host.CallToolAsync(scenario.Tool, arguments, cancellationToken);
        ThrowIfScenarioFailed(scenario.Id, result);

        return result;
    }

    private static void ThrowIfScenarioFailed(string scenarioId, CallToolResult result)
    {
        if (result.IsError == true)
        {
            throw new InvalidOperationException(
                $"Scenario '{scenarioId}' returned an MCP error: {GetStructuredContent(result)}");
        }
    }

    private async Task InvokeSupportingCallsAsync(
        IReadOnlyList<ToolCallDefinition> calls,
        CancellationToken cancellationToken)
    {
        foreach (var call in calls)
        {
            await InvokeRequiredAsync(call.Tool, call.Arguments, cancellationToken);
        }
    }

    private async Task InvokeRequiredAsync(
        string tool,
        JsonElement argumentDefinition,
        CancellationToken cancellationToken)
    {
        var arguments = ArgumentMaterializer.Materialize(argumentDefinition, _workspaceId, _repositoryRoot);
        await InvokeRequiredAsync(tool, arguments, cancellationToken);
    }

    private async Task InvokeRequiredAsync(
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var result = await _host.CallToolAsync(tool, arguments, cancellationToken);
        if (result.IsError == true)
        {
            throw new InvalidOperationException($"Supporting tool '{tool}' returned an MCP error: {GetStructuredContent(result)}");
        }
    }

    private async Task VerifyExclusiveLeaseRecoveryAsync(CancellationToken cancellationToken)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["workspace"] = new Dictionary<string, object?>
            {
                ["workspaceId"] = _workspaceId,
            },
        };

        var recoveryStopwatch = Stopwatch.StartNew();
        while (recoveryStopwatch.Elapsed < _exclusiveLeaseRecoveryTimeout)
        {
            var result = await _host.CallToolAsync("transaction-start", arguments, cancellationToken);
            if (result.IsError != true)
            {
                await InvokeRequiredAsync("transaction-rollback", arguments, CancellationToken.None);
                return;
            }

            if (!IsWorkspaceBusy(result))
            {
                throw new InvalidOperationException(
                    $"Supporting tool 'transaction-start' returned an MCP error: {GetStructuredContent(result)}");
            }

            await Task.Delay(_exclusiveLeaseRetryDelay, cancellationToken);
        }

        throw new TimeoutException(
            $"The workspace did not release its query lease within {_exclusiveLeaseRecoveryTimeout.TotalSeconds:F0} seconds after cancellation.");
    }

    private static bool IsWorkspaceBusy(CallToolResult result)
    {
        if (result.StructuredContent is not JsonElement structuredContent
            || !structuredContent.TryGetProperty("error", out var error)
            || error.ValueKind != JsonValueKind.Object
            || !error.TryGetProperty("code", out var code))
        {
            return false;
        }

        return string.Equals(code.GetString(), "WorkspaceBusy", StringComparison.Ordinal);
    }

    private static string GetStructuredContent(CallToolResult result)
    {
        if (result.StructuredContent is JsonElement structuredContent)
        {
            return structuredContent.GetRawText();
        }

        return JsonSerializer.Serialize(result.Content);
    }
}
