using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.Performance;

internal sealed class DurableCommitRunner
{
    private readonly PerformanceHost _host;
    private readonly string _repositoryRoot;
    private readonly string _workspaceId;

    public DurableCommitRunner(
        PerformanceHost host,
        string workspaceId,
        string repositoryRoot)
    {
        _host = host;
        _workspaceId = workspaceId;
        _repositoryRoot = repositoryRoot;
    }

    public async Task<DurableCommitExecution> ExecuteAsync(
        ScenarioDefinition scenario,
        CancellationToken cancellationToken)
    {
        var preparation = await PrepareAsync(scenario, cancellationToken);
        return await CommitAsync(preparation, cancellationToken);
    }

    public async Task<DurableCommitPreparation> PrepareAsync(
        ScenarioDefinition scenario,
        CancellationToken cancellationToken)
    {
        foreach (var call in scenario.Setup)
        {
            await InvokeRequiredAsync(call.Tool, call.Arguments, cancellationToken);
        }

        var stagingStopwatch = Stopwatch.StartNew();
        var mutationResult = await InvokeRequiredAsync(
            scenario.Tool,
            scenario.Arguments,
            cancellationToken);
        stagingStopwatch.Stop();

        var mutationObservation = ResponseObservation.Create(mutationResult);
        if (mutationObservation.MutationStaged != true)
        {
            throw new InvalidOperationException(
                $"Durable commit scenario '{scenario.Id}' did not stage a mutation.");
        }

        var workspaceArguments = CreateWorkspaceArguments();
        var previewStopwatch = Stopwatch.StartNew();
        var previewResult = await InvokeRequiredAsync(
            "transaction-preview",
            workspaceArguments,
            cancellationToken);
        previewStopwatch.Stop();

        var changedDocumentPaths = GetChangedDocumentPaths(previewResult);
        return new DurableCommitPreparation
        {
            StagingMilliseconds = stagingStopwatch.Elapsed.TotalMilliseconds,
            PreviewMilliseconds = previewStopwatch.Elapsed.TotalMilliseconds,
            PreviewDocumentCount = changedDocumentPaths.Count,
            ChangedDocumentPaths = changedDocumentPaths,
        };
    }

    public async Task<DurableCommitExecution> CommitAsync(
        DurableCommitPreparation preparation,
        CancellationToken cancellationToken)
    {
        var workspaceArguments = CreateWorkspaceArguments();
        var before = _host.CaptureSnapshot();
        var commitStopwatch = Stopwatch.StartNew();
        var commitResult = await InvokeRequiredAsync(
            "transaction-commit",
            workspaceArguments,
            cancellationToken);
        commitStopwatch.Stop();
        var after = _host.CaptureSnapshot();

        EnsureCommitted(commitResult);
        var commitObservation = ResponseObservation.Create(commitResult);
        return new DurableCommitExecution
        {
            StagingMilliseconds = preparation.StagingMilliseconds,
            PreviewMilliseconds = preparation.PreviewMilliseconds,
            CommitMilliseconds = commitStopwatch.Elapsed.TotalMilliseconds,
            CommitHostCpuMilliseconds = (after.CpuTime - before.CpuTime).TotalMilliseconds,
            WorkingSetBytes = after.WorkingSetBytes,
            PeakWorkingSetBytes = after.PeakWorkingSetBytes,
            CommitResponseBytes = commitObservation.Bytes,
            CommitResponseSha256 = commitObservation.Sha256,
            PreviewDocumentCount = preparation.PreviewDocumentCount,
        };
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        await InvokeRequiredAsync(
            "transaction-rollback",
            CreateWorkspaceArguments(),
            cancellationToken);
    }

    private Dictionary<string, object?> CreateWorkspaceArguments()
    {
        return new Dictionary<string, object?>
        {
            ["workspace"] = new Dictionary<string, object?>
            {
                ["workspaceId"] = _workspaceId,
            },
        };
    }

    private async Task<CallToolResult> InvokeRequiredAsync(
        string tool,
        JsonElement argumentDefinition,
        CancellationToken cancellationToken)
    {
        var arguments = ArgumentMaterializer.Materialize(
            argumentDefinition,
            _workspaceId,
            _repositoryRoot);

        return await InvokeRequiredAsync(tool, arguments, cancellationToken);
    }

    private async Task<CallToolResult> InvokeRequiredAsync(
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var result = await _host.CallToolAsync(tool, arguments, cancellationToken);
        if (result.IsError == true)
        {
            throw new InvalidOperationException(
                $"Tool '{tool}' returned an MCP error: {result.StructuredContent?.GetRawText()}");
        }

        return result;
    }

    private static List<string> GetChangedDocumentPaths(CallToolResult result)
    {
        var content = result.StructuredContent
            ?? throw new InvalidDataException("transaction-preview returned no structured content.");
        var documents = content
            .GetProperty("data")
            .GetProperty("documents");

        var paths = new List<string>(documents.GetArrayLength());
        foreach (var document in documents.EnumerateArray())
        {
            var path = document
                .GetProperty("document")
                .GetProperty("path")
                .GetString();

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidDataException(
                    "transaction-preview returned a changed document without a path.");
            }

            paths.Add(path);
        }

        return paths;
    }

    private static void EnsureCommitted(CallToolResult result)
    {
        var content = result.StructuredContent
            ?? throw new InvalidDataException("transaction-commit returned no structured content.");
        var committed = content
            .GetProperty("data")
            .GetProperty("committed");
        if (committed.ValueKind != JsonValueKind.True)
        {
            throw new InvalidOperationException("transaction-commit completed without committing the staged mutation.");
        }
    }
}
