using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.Performance;

internal sealed class ScenarioRunner
{
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

            measurements.Add(new InvocationMeasurement
            {
                Iteration = iteration,
                ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                HostCpuMilliseconds = (after.CpuTime - before.CpuTime).TotalMilliseconds,
                WorkingSetBytes = after.WorkingSetBytes,
                WorkingSetDeltaBytes = after.WorkingSetBytes - before.WorkingSetBytes,
                PeakWorkingSetBytes = after.PeakWorkingSetBytes,
                ResponseBytes = GetResponseSize(result),
                IsError = result.IsError == true,
            });
        }

        return measurements;
    }

    public async Task<int> RunUntilExitAsync(
        ScenarioDefinition scenario,
        Process diagnosticProcess,
        CancellationToken cancellationToken)
    {
        var invocationCount = 0;
        while (!diagnosticProcess.HasExited)
        {
            await InvokeScenarioAsync(scenario, cancellationToken);
            invocationCount++;
        }

        return invocationCount;
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
        if (result.IsError == true)
        {
            throw new InvalidOperationException(
                $"Scenario '{scenario.Id}' returned an MCP error: {GetStructuredContent(result)}");
        }

        return result;
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
        var result = await _host.CallToolAsync(tool, arguments, cancellationToken);
        if (result.IsError == true)
        {
            throw new InvalidOperationException($"Supporting tool '{tool}' returned an MCP error: {GetStructuredContent(result)}");
        }
    }

    private static int GetResponseSize(CallToolResult result)
    {
        var content = GetStructuredContent(result);
        return Encoding.UTF8.GetByteCount(content);
    }

    private static string GetStructuredContent(CallToolResult result)
    {
        return result.StructuredContent?.GetRawText() ?? JsonSerializer.Serialize(result.Content);
    }
}
