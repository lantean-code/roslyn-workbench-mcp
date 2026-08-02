using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;

internal sealed class DurableCommitRunner
{
    private readonly ScenarioHost _host;
    private readonly CodeActionWorkflowInvoker _codeActionWorkflow;
    private readonly Guid _workspaceId;

    public DurableCommitRunner(
        ScenarioHost host,
        Guid workspaceId,
        string repositoryRoot)
    {
        _host = host;
        _workspaceId = workspaceId;
        _codeActionWorkflow = new CodeActionWorkflowInvoker(host, workspaceId, repositoryRoot);
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
            await InvokeRequiredAsync(call, cancellationToken);
        }

        var stagingStopwatch = Stopwatch.StartNew();
        var mutationResult = await InvokeRequiredAsync(
            scenario.Tool,
            scenario.Arguments,
            scenario.CodeActionSelection,
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

        var (previewDocumentCount, changedTargets) = GetPreviewChanges(previewResult);
        return new DurableCommitPreparation
        {
            StagingMilliseconds = stagingStopwatch.Elapsed.TotalMilliseconds,
            PreviewMilliseconds = previewStopwatch.Elapsed.TotalMilliseconds,
            PreviewDocumentCount = previewDocumentCount,
            ChangedTargets = changedTargets,
        };
    }

    public async Task<DurableCommitExecution> CommitAsync(
        DurableCommitPreparation preparation,
        CancellationToken cancellationToken)
    {
        var workspaceArguments = CreateMutationArguments(transactionRevision: 1);
        var before = _host.CaptureSnapshot();
        await using var memorySampler = _host.StartMemorySampling();
        var commitStopwatch = Stopwatch.StartNew();
        var commitResult = await InvokeRequiredAsync(
            "transaction-commit",
            workspaceArguments,
            cancellationToken);
        commitStopwatch.Stop();
        var commitMemory = await memorySampler.CompleteAsync();
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
            CommitMemory = commitMemory,
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

    private async Task<CallToolResult> InvokeRequiredAsync(
        string tool,
        JsonElement argumentDefinition,
        CodeActionSelectionDefinition? selection,
        CancellationToken cancellationToken)
    {
        var result = await _codeActionWorkflow.InvokeAsync(
            tool,
            argumentDefinition,
            selection,
            cancellationToken);
        if (result.IsError == true)
        {
            throw new InvalidOperationException(
                $"Tool '{tool}' returned an MCP error: {result.StructuredContent?.GetRawText()}");
        }

        return result;
    }

    private Task<CallToolResult> InvokeRequiredAsync(
        ToolCallDefinition call,
        CancellationToken cancellationToken)
    {
        return InvokeRequiredAsync(
            call.Tool,
            call.Arguments,
            call.CodeActionSelection,
            cancellationToken);
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

    private static (int DocumentCount, List<DurableCommitTarget> Targets) GetPreviewChanges(
        CallToolResult result)
    {
        var content = result.StructuredContent
            ?? throw new InvalidDataException("transaction-preview returned no structured content.");
        var documents = content
            .GetProperty("data")
            .GetProperty("documents");

        var targets = new Dictionary<string, DurableCommitFileOperation>(
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);

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

            var operation = ParseOperation(document.GetProperty("changeKind"));
            if (targets.TryGetValue(path, out var existingOperation)
                && existingOperation != operation)
            {
                throw new InvalidDataException(
                    $"transaction-preview returned conflicting operations for '{path}'.");
            }

            targets.TryAdd(path, operation);
        }

        var changedTargets = targets
            .Select(static target => new DurableCommitTarget
            {
                Path = target.Key,
                Operation = target.Value,
            })
            .ToList();

        return (documents.GetArrayLength(), changedTargets);
    }

    private static DurableCommitFileOperation ParseOperation(JsonElement value)
    {
        return value.GetString() switch
        {
            "Added" => DurableCommitFileOperation.Create,
            "Modified" => DurableCommitFileOperation.Replace,
            "Deleted" => DurableCommitFileOperation.Delete,
            var operation => throw new InvalidDataException(
                $"transaction-preview returned unsupported change kind '{operation}'."),
        };
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
