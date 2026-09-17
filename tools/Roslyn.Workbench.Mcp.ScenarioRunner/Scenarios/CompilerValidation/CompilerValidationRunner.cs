using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CompilerValidation;

internal sealed class CompilerValidationRunner
{
    private readonly DurableCommitRunner _durableCommitRunner;
    private readonly ScenarioHost _host;
    private readonly Guid _workspaceId;

    public CompilerValidationRunner(
        ScenarioHost host,
        Guid workspaceId,
        string repositoryRoot)
    {
        _host = host;
        _workspaceId = workspaceId;
        _durableCommitRunner = new DurableCommitRunner(host, workspaceId, repositoryRoot);
    }

    public async Task<CompilerValidationExecution> ExecuteAsync(
        ScenarioDefinition scenario,
        CancellationToken cancellationToken)
    {
        var preparation = await _durableCommitRunner.PrepareAsync(
            scenario,
            cancellationToken);

        var coldValidation = await MeasureValidationAsync(cancellationToken);
        var repeatedValidation = await MeasureValidationAsync(cancellationToken);

        return new CompilerValidationExecution
        {
            StagingMilliseconds = preparation.StagingMilliseconds,
            PreviewMilliseconds = preparation.PreviewMilliseconds,
            PreviewDocumentCount = preparation.PreviewDocumentCount,
            ColdValidation = coldValidation,
            RepeatedValidation = repeatedValidation,
        };
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        return _durableCommitRunner.RollbackAsync(cancellationToken);
    }

    private async Task<CompilerValidationInvocationMeasurement> MeasureValidationAsync(
        CancellationToken cancellationToken)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["workspace"] = new Dictionary<string, object?>
            {
                ["workspaceId"] = _workspaceId,
            },
            ["expectedSnapshot"] = _host.GetSnapshot(_workspaceId),
        };

        var before = _host.CaptureSnapshot();
        var stopwatch = Stopwatch.StartNew();
        var result = await _host.CallToolAsync(
            "transaction-validate",
            arguments,
            cancellationToken);

        stopwatch.Stop();
        var after = _host.CaptureSnapshot();
        var data = ReadValidation(result);
        var observation = ResponseObservation.Create(result);

        return new CompilerValidationInvocationMeasurement
        {
            ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            ReportedDurationMilliseconds = data.GetProperty("durationMilliseconds").GetInt64(),
            HostCpuMilliseconds = (after.CpuTime - before.CpuTime).TotalMilliseconds,
            WorkingSetBytes = after.WorkingSetBytes,
            WorkingSetDeltaBytes = after.WorkingSetBytes - before.WorkingSetBytes,
            PeakWorkingSetBytes = after.PeakWorkingSetBytes,
            ResponseBytes = observation.Bytes,
            AffectedProjectCount = data.GetProperty("projects").GetArrayLength(),
            IntroducedErrorCount = data.GetProperty("introducedErrorCount").GetInt32(),
            IntroducedDiagnosticCount = data.GetProperty("introducedDiagnostics").GetArrayLength(),
            IntroducedDiagnosticIds = CountIntroducedDiagnosticIds(data),
            ProjectsWithIntroducedErrors = ReadProjectsWithIntroducedErrors(data),
            IsComplete = data.GetProperty("isComplete").GetBoolean(),
            Succeeded = data.GetProperty("succeeded").GetBoolean(),
            Limitations = data.GetProperty("limitations")
                .EnumerateArray()
                .Select(static limitation => limitation.GetString() ?? string.Empty)
                .ToArray(),
        };
    }

    private static Dictionary<string, int> CountIntroducedDiagnosticIds(JsonElement data)
    {
        return data.GetProperty("introducedDiagnostics")
            .EnumerateArray()
            .GroupBy(static diagnostic => diagnostic.GetProperty("id").GetString() ?? string.Empty)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
    }

    private static SortedDictionary<string, int> ReadProjectsWithIntroducedErrors(JsonElement data)
    {
        var projects = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var project in data.GetProperty("projects").EnumerateArray())
        {
            var introducedErrorCount = project.GetProperty("introducedErrorCount").GetInt32();
            if (introducedErrorCount == 0)
            {
                continue;
            }

            var projectName = project.GetProperty("project").GetString() ?? string.Empty;
            var targetFramework = project.GetProperty("targetFramework").GetString();
            var projectIdentity = projectName;
            if (!string.IsNullOrWhiteSpace(targetFramework))
            {
                projectIdentity = $"{projectName} ({targetFramework})";
            }

            projects.Add(projectIdentity, introducedErrorCount);
        }

        return projects;
    }

    private static JsonElement ReadValidation(CallToolResult result)
    {
        if (result.IsError == true)
        {
            throw new InvalidOperationException(
                $"transaction-validate returned an MCP error: {result.StructuredContent?.GetRawText()}");
        }

        var content = result.StructuredContent
            ?? throw new InvalidDataException("transaction-validate returned no structured content.");
        return content.GetProperty("data");
    }
}
