using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;
using Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CommitCancellation;

internal sealed class CommitCancellationRunner
{
    private static readonly TimeSpan _settlementRetryDelay =
        TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan _settlementTimeout =
        TimeSpan.FromSeconds(30);
    private readonly ScenarioHost _host;
    private readonly string _repositoryRoot;
    private readonly string _stateDirectory;
    private readonly string _workspaceId;

    public bool HasOpenTransaction { get; private set; }

    public CommitCancellationRunner(
        ScenarioHost host,
        string workspaceId,
        string repositoryRoot,
        string stateDirectory)
    {
        _host = host;
        _workspaceId = workspaceId;
        _repositoryRoot = repositoryRoot;
        _stateDirectory = stateDirectory;
    }

    public async Task<CommitCancellationExecution> ExecuteAsync(
        ScenarioDefinition scenario,
        CommitCancellationBoundary boundary,
        CancellationToken cancellationToken)
    {
        var durableRunner = new DurableCommitRunner(
            _host,
            _workspaceId,
            _repositoryRoot);
        HasOpenTransaction = true;
        var preparation = await durableRunner.PrepareAsync(
            scenario,
            cancellationToken);

        var phase = boundary == CommitCancellationBoundary.BeforeApplying
            ? "Staging"
            : "Applying";
        var monitor = new WorkspaceCommitPhaseMonitor(_repositoryRoot);
        var (requestId, invocation) = _host.StartCancellableToolCall(
            "transaction-commit",
            CreateMutationArguments(transactionRevision: 1),
            cancellationToken);

        monitor.WaitForPhase(phase, invocation, cancellationToken);

        var notificationStopwatch = Stopwatch.StartNew();
        await _host.CancelToolCallAsync(requestId, cancellationToken);
        notificationStopwatch.Stop();

        var completionStopwatch = Stopwatch.StartNew();
        var operationCanceled = false;
        var committed = false;
        try
        {
            var result = await invocation;
            committed = GetCommitted(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            operationCanceled = true;
        }

        completionStopwatch.Stop();

        var settlementStopwatch = Stopwatch.StartNew();
        int? postCancellationPreviewDocumentCount = null;
        if (boundary == CommitCancellationBoundary.BeforeApplying)
        {
            if (!operationCanceled || committed)
            {
                throw new InvalidOperationException(
                    "A commit cancelled during Staging did not report cancellation before source application.");
            }

            postCancellationPreviewDocumentCount = await GetPreviewDocumentCountAsync(
                cancellationToken);

            await RollbackAsync(CancellationToken.None);
        }
        else
        {
            if (!operationCanceled && !committed)
            {
                throw new InvalidOperationException(
                    "A commit cancelled during Applying did not continue to durable completion.");
            }

            monitor.WaitForTerminalPhase(
                "Committed",
                cancellationToken);

            committed = true;
            HasOpenTransaction = false;
        }

        settlementStopwatch.Stop();
        var recoveryEvidence = await RecoveryEvidenceReader.ReadAsync(
            _stateDirectory,
            cancellationToken);
        if (recoveryEvidence.State is not null
            || recoveryEvidence.ArtifactCount != 0)
        {
            throw new InvalidOperationException(
                "Commit cancellation left unfinished recovery state.");
        }

        return new CommitCancellationExecution
        {
            Boundary = boundary,
            ObservedPhase = phase,
            StagingMilliseconds = preparation.StagingMilliseconds,
            PreviewMilliseconds = preparation.PreviewMilliseconds,
            CancellationNotificationMilliseconds =
                notificationStopwatch.Elapsed.TotalMilliseconds,
            CompletionAfterCancellationMilliseconds =
                completionStopwatch.Elapsed.TotalMilliseconds,
            SettlementMilliseconds =
                settlementStopwatch.Elapsed.TotalMilliseconds,
            OperationCanceled = operationCanceled,
            Committed = committed,
            PreviewDocumentCount = preparation.PreviewDocumentCount,
            PostCancellationPreviewDocumentCount =
                postCancellationPreviewDocumentCount,
            RecoveryEvidence = recoveryEvidence,
        };
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (!HasOpenTransaction)
        {
            return;
        }

        var result = await InvokeAfterSettlementAsync(
            "transaction-rollback",
            cancellationToken);

        if (result.IsError == true)
        {
            throw new InvalidOperationException(
                $"transaction-rollback returned an MCP error: {result.StructuredContent?.GetRawText()}");
        }

        HasOpenTransaction = false;
    }

    private async Task<int> GetPreviewDocumentCountAsync(
        CancellationToken cancellationToken)
    {
        var result = await InvokeAfterSettlementAsync(
            "transaction-preview",
            cancellationToken);

        if (result.IsError == true
            || result.StructuredContent is not JsonElement content)
        {
            throw new InvalidOperationException(
                "The transaction was not available for preview after pre-application cancellation.");
        }

        var documents = content
            .GetProperty("data")
            .GetProperty("documents");
        var count = documents.GetArrayLength();
        if (count == 0)
        {
            throw new InvalidOperationException(
                "Pre-application cancellation did not preserve the staged transaction.");
        }

        return count;
    }

    private async Task<CallToolResult> InvokeAfterSettlementAsync(
        string tool,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < _settlementTimeout)
        {
            var result = await _host.CallToolAsync(
                tool,
                CreateWorkspaceArguments(),
                cancellationToken);

            if (!IsWorkspaceBusy(result))
            {
                return result;
            }

            await Task.Delay(_settlementRetryDelay, cancellationToken);
        }

        throw new TimeoutException(
            $"The commit lease did not settle within {_settlementTimeout.TotalSeconds:F0} seconds after cancellation.");
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

    private static bool GetCommitted(CallToolResult result)
    {
        if (result.IsError == true
            || result.StructuredContent is not JsonElement content)
        {
            return false;
        }

        return content
            .GetProperty("data")
            .GetProperty("committed")
            .ValueKind == JsonValueKind.True;
    }

    private static bool IsWorkspaceBusy(CallToolResult result)
    {
        if (result.IsError != true
            || result.StructuredContent is not JsonElement content
            || !content.TryGetProperty("error", out var error)
            || !error.TryGetProperty("code", out var code))
        {
            return false;
        }

        return string.Equals(
            code.GetString(),
            "WorkspaceBusy",
            StringComparison.Ordinal);
    }
}
