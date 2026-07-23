using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.StateSequences;

internal sealed class StateSequenceRunner
{
    private readonly ScenarioHost _host;
    private readonly string _repositoryRoot;
    private readonly string _workspaceId;

    public StateSequenceRunner(
        ScenarioHost host,
        string workspaceId,
        string repositoryRoot)
    {
        _host = host;
        _workspaceId = workspaceId;
        _repositoryRoot = repositoryRoot;
    }

    public Task<StateSequenceExecution> ExecuteAsync(
        ScenarioDefinition scenario,
        CancellationToken cancellationToken)
    {
        var definition = scenario.StateSequence
            ?? throw new InvalidOperationException(
                $"Scenario '{scenario.Id}' does not define a state sequence.");

        return definition.Kind switch
        {
            StateSequenceKind.ExternalReload => ExecuteExternalReloadAsync(
                scenario,
                definition,
                cancellationToken),
            StateSequenceKind.MultiRevisionCommit => ExecuteMultiRevisionCommitAsync(
                scenario,
                definition,
                cancellationToken),
            _ => throw new InvalidDataException(
                $"Scenario '{scenario.Id}' has unknown state sequence kind '{definition.Kind}'."),
        };
    }

    private async Task<StateSequenceExecution> ExecuteExternalReloadAsync(
        ScenarioDefinition scenario,
        StateSequenceDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.Mutations.Count > 0)
        {
            throw new InvalidDataException(
                $"External-reload scenario '{scenario.Id}' must not define transaction mutations.");
        }

        var externalMutation = definition.ExternalMutation
            ?? throw new InvalidDataException(
                $"External-reload scenario '{scenario.Id}' does not define an external mutation.");
        var steps = new List<StateSequenceStepMeasurement>();
        var queryArguments = Materialize(scenario.Arguments);

        var baseline = await InvokeRequiredAsync(
            "baseline-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(baseline);
        var cached = await InvokeRequiredAsync(
            "cached-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(cached);
        ValidateEquivalentQueries(baseline, cached);

        await using var insertion = await ExternalMemberInsertion.ApplyAsync(
            _repositoryRoot,
            externalMutation,
            cancellationToken);

        var stale = await InvokeAsync(
            "stale-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(stale);
        if (!stale.IsError
            || !string.Equals(
                stale.ErrorCode,
                "WorkspaceOutOfDate",
                StringComparison.Ordinal)
            || !string.Equals(
                stale.RequiredAction,
                "ReloadWorkspace",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The externally changed workspace did not reject the stale query with WorkspaceOutOfDate and ReloadWorkspace.");
        }

        var reload = await InvokeRequiredAsync(
            "workspace-reload",
            "workspace-reload",
            CreateWorkspaceArguments(),
            cancellationToken);

        steps.Add(reload);
        var refreshed = await InvokeRequiredAsync(
            "refreshed-query",
            scenario.Tool,
            queryArguments,
            cancellationToken);

        steps.Add(refreshed);
        ValidateExternalRefresh(baseline, refreshed);

        return new StateSequenceExecution { Steps = steps };
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A failed multi-revision manual scenario must attempt transaction rollback before preserving and rethrowing the original failure.")]
    private async Task<StateSequenceExecution> ExecuteMultiRevisionCommitAsync(
        ScenarioDefinition scenario,
        StateSequenceDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.ExternalMutation is not null)
        {
            throw new InvalidDataException(
                $"Multi-revision scenario '{scenario.Id}' must not define an external mutation.");
        }

        if (definition.Mutations.Count != 2)
        {
            throw new InvalidDataException(
                $"Multi-revision scenario '{scenario.Id}' must define exactly two mutations.");
        }

        var steps = new List<StateSequenceStepMeasurement>();
        var queryArguments = Materialize(scenario.Arguments);
        var transactionStarted = false;
        try
        {
            var baseline = await InvokeRequiredAsync(
                "baseline-query",
                scenario.Tool,
                queryArguments,
                cancellationToken);

            steps.Add(baseline);
            var cached = await InvokeRequiredAsync(
                "cached-query",
                scenario.Tool,
                queryArguments,
                cancellationToken);

            steps.Add(cached);
            ValidateEquivalentQueries(baseline, cached);

            var start = await InvokeRequiredAsync(
                "transaction-start",
                "transaction-start",
                CreateWorkspaceArguments(),
                cancellationToken);

            steps.Add(start);
            transactionStarted = true;

            for (var index = 0; index < definition.Mutations.Count; index++)
            {
                var mutation = definition.Mutations[index];
                var step = await InvokeRequiredAsync(
                    $"mutation-{index + 1}",
                    mutation.Tool,
                    Materialize(mutation.Arguments),
                    cancellationToken);

                steps.Add(step);
                if (step.MutationStaged != true)
                {
                    throw new InvalidOperationException(
                        $"Mutation {index + 1} did not report a staged change.");
                }
            }

            var preview = await InvokeRequiredAsync(
                "transaction-preview",
                "transaction-preview",
                CreateWorkspaceArguments(),
                cancellationToken);

            steps.Add(preview);
            ValidateTransactionState(
                preview,
                expectedRevision: 2,
                expectedRevisionCount: 2);

            var undo = await InvokeRequiredAsync(
                "history-undo",
                "transaction-history",
                CreateHistoryArguments("Undo"),
                cancellationToken);

            steps.Add(undo);
            ValidateTransactionState(
                undo,
                expectedRevision: 1,
                expectedRevisionCount: 2,
                expectedCanRedo: true);

            var redo = await InvokeRequiredAsync(
                "history-redo",
                "transaction-history",
                CreateHistoryArguments("Redo"),
                cancellationToken);

            steps.Add(redo);
            ValidateTransactionState(
                redo,
                expectedRevision: 2,
                expectedRevisionCount: 2,
                expectedCanUndo: true);

            var commit = await InvokeRequiredAsync(
                "transaction-commit",
                "transaction-commit",
                CreateWorkspaceArguments(),
                cancellationToken);

            steps.Add(commit);
            transactionStarted = false;

            var refreshed = await InvokeRequiredAsync(
                "post-commit-query",
                scenario.Tool,
                queryArguments,
                cancellationToken);

            steps.Add(refreshed);
            ValidateCommittedRefresh(baseline, refreshed);

            return new StateSequenceExecution { Steps = steps };
        }
        catch
        {
            if (transactionStarted)
            {
                _ = await InvokeAsync(
                    "failure-rollback",
                    "transaction-rollback",
                    CreateWorkspaceArguments(),
                    CancellationToken.None);
            }

            throw;
        }
    }

    private async Task<StateSequenceStepMeasurement> InvokeRequiredAsync(
        string name,
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var step = await InvokeAsync(
            name,
            tool,
            arguments,
            cancellationToken);

        if (step.IsError)
        {
            throw new InvalidOperationException(
                $"State sequence step '{name}' returned '{step.ErrorCode ?? "an MCP error"}'.");
        }

        return step;
    }

    private async Task<StateSequenceStepMeasurement> InvokeAsync(
        string name,
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await _host.CallToolAsync(
            tool,
            arguments,
            cancellationToken);

        stopwatch.Stop();
        return CreateStep(name, tool, stopwatch.Elapsed.TotalMilliseconds, result);
    }

    private IReadOnlyDictionary<string, object?> Materialize(JsonElement arguments)
    {
        return ArgumentMaterializer.Materialize(
            arguments,
            _workspaceId,
            _repositoryRoot);
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

    private Dictionary<string, object?> CreateHistoryArguments(string direction)
    {
        var arguments = CreateWorkspaceArguments();
        arguments["direction"] = direction;
        return arguments;
    }

    private static StateSequenceStepMeasurement CreateStep(
        string name,
        string tool,
        double elapsedMilliseconds,
        CallToolResult result)
    {
        var observation = ResponseObservation.Create(result);
        var content = result.StructuredContent;
        var errorCode = TryGetString(content, "error", "code");
        var requiredAction = TryGetString(content, "next");
        var transaction = TryGetObject(content, "data", "transaction");
        if (transaction is null)
        {
            transaction = TryGetObject(content, "data", "mutation", "transaction");
        }

        var references = TryGetObject(content, "data", "references");

        return new StateSequenceStepMeasurement
        {
            Name = name,
            Tool = tool,
            ElapsedMilliseconds = elapsedMilliseconds,
            IsError = result.IsError == true,
            ResponseSha256 = observation.Sha256,
            ErrorCode = errorCode,
            RequiredAction = requiredAction,
            MutationStaged = observation.MutationStaged,
            ReferenceCount = GetReferenceCount(references),
            DefinitionPaths = GetDefinitionPaths(references),
            TransactionRevision = TryGetInt32(transaction, "revision"),
            TransactionRevisionCount = TryGetInt32(transaction, "revisionCount"),
            CanUndo = TryGetBoolean(transaction, "canUndo"),
            CanRedo = TryGetBoolean(transaction, "canRedo"),
        };
    }

    private static void ValidateEquivalentQueries(
        StateSequenceStepMeasurement baseline,
        StateSequenceStepMeasurement cached)
    {
        if (!string.Equals(
            baseline.ResponseSha256,
            cached.ResponseSha256,
            StringComparison.Ordinal)
            || baseline.ReferenceCount != cached.ReferenceCount)
        {
            throw new InvalidOperationException(
                "The warmed query did not reproduce the baseline response.");
        }
    }

    private static void ValidateExternalRefresh(
        StateSequenceStepMeasurement baseline,
        StateSequenceStepMeasurement refreshed)
    {
        if (baseline.ReferenceCount is null
            || refreshed.ReferenceCount <= baseline.ReferenceCount
            || string.Equals(
                baseline.ResponseSha256,
                refreshed.ResponseSha256,
                StringComparison.Ordinal))
        {
            var baselineCount = baseline.ReferenceCount?.ToString(
                CultureInfo.InvariantCulture) ?? "unknown";
            var refreshedCount = refreshed.ReferenceCount?.ToString(
                CultureInfo.InvariantCulture) ?? "unknown";
            var responseChanged = !string.Equals(
                baseline.ResponseSha256,
                refreshed.ResponseSha256,
                StringComparison.Ordinal);

            throw new InvalidOperationException(
                $"The reloaded query did not contain externally added semantic references. Baseline count: {baselineCount}; refreshed count: {refreshedCount}; response changed: {responseChanged}.");
        }
    }

    private static void ValidateCommittedRefresh(
        StateSequenceStepMeasurement baseline,
        StateSequenceStepMeasurement refreshed)
    {
        var baselineInOriginalFile = ContainsFileName(
            baseline.DefinitionPaths,
            "Guard.cs");
        var refreshedInCreatedFile = ContainsFileName(
            refreshed.DefinitionPaths,
            "NoEnumerationAttribute.cs");

        if (!baselineInOriginalFile
            || !refreshedInCreatedFile
            || string.Equals(
                baseline.ResponseSha256,
                refreshed.ResponseSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The post-commit query did not resolve the moved definition from the committed solution.");
        }
    }

    private static void ValidateTransactionState(
        StateSequenceStepMeasurement step,
        int expectedRevision,
        int expectedRevisionCount,
        bool? expectedCanUndo = null,
        bool? expectedCanRedo = null)
    {
        if (step.TransactionRevision != expectedRevision
            || step.TransactionRevisionCount != expectedRevisionCount
            || (expectedCanUndo is not null
                && step.CanUndo != expectedCanUndo)
            || (expectedCanRedo is not null
                && step.CanRedo != expectedCanRedo))
        {
            throw new InvalidOperationException(
                $"Step '{step.Name}' did not report the expected transaction history state.");
        }
    }

    private static bool ContainsFileName(
        IReadOnlyList<string> paths,
        string fileName)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return paths.Any(path => string.Equals(
            Path.GetFileName(path),
            fileName,
            comparison));
    }

    private static JsonElement? TryGetObject(
        JsonElement? content,
        params string[] path)
    {
        if (content is not JsonElement value)
        {
            return null;
        }

        foreach (var property in path)
        {
            if (value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty(property, out value))
            {
                return null;
            }
        }

        return value.ValueKind == JsonValueKind.Object
            ? value
            : null;
    }

    private static string? TryGetString(
        JsonElement? content,
        params string[] path)
    {
        if (content is not JsonElement value)
        {
            return null;
        }

        foreach (var property in path)
        {
            if (value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty(property, out value))
            {
                return null;
            }
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? TryGetInt32(
        JsonElement? value,
        string property)
    {
        if (value is not JsonElement element
            || !element.TryGetProperty(property, out var result)
            || !result.TryGetInt32(out var number))
        {
            return null;
        }

        return number;
    }

    private static bool? TryGetBoolean(
        JsonElement? value,
        string property)
    {
        if (value is not JsonElement element
            || !element.TryGetProperty(property, out var result)
            || result.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        return result.GetBoolean();
    }

    private static int? GetReferenceCount(JsonElement? references)
    {
        if (references is not JsonElement element
            || !element.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return items.GetArrayLength();
    }

    private static List<string> GetDefinitionPaths(
        JsonElement? references)
    {
        if (references is not JsonElement element
            || !element.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var paths = new List<string>();
        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("isDefinition", out var isDefinition)
                || isDefinition.ValueKind != JsonValueKind.True
                || !item.TryGetProperty("location", out var location)
                || !location.TryGetProperty("document", out var document)
                || !document.TryGetProperty("path", out var path)
                || path.GetString() is not { } value)
            {
                continue;
            }

            paths.Add(value);
        }

        return paths;
    }
}
