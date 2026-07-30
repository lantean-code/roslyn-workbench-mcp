using System.Text.Json;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

internal sealed class CodeActionWorkflowInvoker
{
    private readonly Dictionary<string, Guid> _capturedReferences = new(StringComparer.Ordinal);
    private readonly ScenarioHost _host;
    private readonly string _repositoryRoot;
    private readonly string _workspaceId;

    public CodeActionWorkflowInvoker(
        ScenarioHost host,
        string workspaceId,
        string repositoryRoot)
    {
        _host = host;
        _workspaceId = workspaceId;
        _repositoryRoot = repositoryRoot;
    }

    public async Task<CallToolResult> InvokeAsync(
        string tool,
        JsonElement argumentDefinition,
        CodeActionSelectionDefinition? selection,
        CancellationToken cancellationToken)
    {
        var arguments = Materialize(argumentDefinition);
        if (selection is null)
        {
            return await _host.CallToolAsync(tool, arguments, cancellationToken);
        }

        if (selection.UseCaptured is not null)
        {
            ValidateCapturedReferenceUse(tool, selection);
            arguments["actionId"] = GetCapturedReference(selection.UseCaptured);
            var capturedReferenceResult = await _host.CallToolAsync(tool, arguments, cancellationToken);
            if (selection.CaptureAs is not null)
            {
                CaptureReference(
                    selection.CaptureAs,
                    GetResultActionId(capturedReferenceResult, tool));
            }

            return capturedReferenceResult;
        }

        if (string.Equals(tool, "list-code-actions", StringComparison.Ordinal))
        {
            ValidateDiscoverySelection(selection, requireCaptureName: true);
            var listResult = await _host.CallToolAsync(tool, arguments, cancellationToken);
            var actionId = SelectActionId(listResult, selection);
            CaptureReference(GetRequiredCaptureName(selection), actionId);
            return listResult;
        }

        if (!string.Equals(tool, "stage-code-action", StringComparison.Ordinal)
            && !string.Equals(tool, "prepare-fix-all", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A Code Action selection can only be used with list-code-actions, prepare-fix-all or stage-code-action.");
        }

        ValidateDiscoverySelection(
            selection,
            requireCaptureName: string.Equals(tool, "prepare-fix-all", StringComparison.Ordinal));
        arguments["actionId"] = await DiscoverActionIdAsync(selection, cancellationToken);
        var result = await _host.CallToolAsync(tool, arguments, cancellationToken);
        if (selection.CaptureAs is not null)
        {
            CaptureReference(selection.CaptureAs, GetResultActionId(result, tool));
        }

        return result;
    }

    private async Task<Guid> DiscoverActionIdAsync(
        CodeActionSelectionDefinition selection,
        CancellationToken cancellationToken)
    {
        var listResult = await _host.CallToolAsync(
            "list-code-actions",
            Materialize(selection.Arguments),
            cancellationToken);

        return SelectActionId(listResult, selection);
    }

    private static Guid SelectActionId(
        CallToolResult listResult,
        CodeActionSelectionDefinition selection)
    {
        if (listResult.IsError == true)
        {
            throw new InvalidOperationException(
                $"Code Action discovery returned an MCP error: {listResult.StructuredContent?.GetRawText()}");
        }

        var content = listResult.StructuredContent
            ?? throw new InvalidDataException("list-code-actions returned no structured content.");
        var items = content
            .GetProperty("data")
            .GetProperty("actions")
            .GetProperty("items");

        var matches = new List<JsonElement>();
        foreach (var item in items.EnumerateArray())
        {
            if (Matches(item, selection))
            {
                matches.Add(item);
            }
        }

        if (matches.Count != 1)
        {
            var availableActions = items
                .EnumerateArray()
                .Select(FormatActionSummary);

            throw new InvalidOperationException(
                $"Code Action selection '{selection.TitleContains}' matched {matches.Count} actions. Available actions: {string.Join("; ", availableActions)}.");
        }

        return matches[0].GetProperty("actionId").GetGuid();
    }

    private void CaptureReference(string name, Guid actionId)
    {
        _capturedReferences[name] = actionId;
    }

    private Guid GetCapturedReference(string name)
    {
        if (!_capturedReferences.TryGetValue(name, out var actionId))
        {
            throw new InvalidDataException(
                $"Code Action reference name '{name}' has not been captured.");
        }

        return actionId;
    }

    private Dictionary<string, object?> Materialize(JsonElement arguments)
    {
        var materialized = ArgumentMaterializer.Materialize(
            arguments,
            _workspaceId,
            _repositoryRoot,
            _host.GetWorkspaceEpoch(_workspaceId));

        return new Dictionary<string, object?>(materialized, StringComparer.Ordinal);
    }

    private static bool Matches(JsonElement item, CodeActionSelectionDefinition selection)
    {
        var title = item.GetProperty("title").GetString();
        if (selection.TitleContains is not null
            && (title is null
                || !title.Contains(selection.TitleContains, StringComparison.Ordinal)))
        {
            return false;
        }

        if (selection.DiagnosticId is not null
            && !HasDiagnostic(item, selection.DiagnosticId))
        {
            return false;
        }

        return selection.Location is null
            || HasLocation(item, selection.Location);
    }

    private static bool HasDiagnostic(JsonElement item, string diagnosticId)
    {
        if (!item.TryGetProperty("diagnostics", out var diagnostics)
            || !diagnostics.TryGetProperty("items", out var items))
        {
            return false;
        }

        foreach (var diagnostic in items.EnumerateArray())
        {
            if (string.Equals(
                diagnostic.GetProperty("id").GetString(),
                diagnosticId,
                StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLocation(
        JsonElement item,
        CodeActionSelectionLocation expected)
    {
        var location = item.GetProperty("location");
        var document = location.GetProperty("document");
        var span = location.GetProperty("span");

        return string.Equals(
                document.GetProperty("path").GetString(),
                expected.Path,
                StringComparison.Ordinal)
            && span.GetProperty("start").GetInt32() == expected.Start
            && (expected.Length is null
                || span.GetProperty("length").GetInt32() == expected.Length);
    }

    private static string FormatActionSummary(JsonElement item)
    {
        var title = item.GetProperty("title").GetString() ?? "<untitled>";
        var location = item.GetProperty("location");
        var document = location.GetProperty("document");
        var span = location.GetProperty("span");
        var diagnosticIds = new List<string>();
        if (item.TryGetProperty("diagnostics", out var diagnostics)
            && diagnostics.TryGetProperty("items", out var diagnosticItems))
        {
            foreach (var diagnostic in diagnosticItems.EnumerateArray())
            {
                var diagnosticId = diagnostic.GetProperty("id").GetString();
                if (diagnosticId is not null)
                {
                    diagnosticIds.Add(diagnosticId);
                }
            }
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{title} [{string.Join(",", diagnosticIds)}] at {document.GetProperty("path").GetString()}:{span.GetProperty("start").GetInt32()}+{span.GetProperty("length").GetInt32()}");
    }

    private static Guid GetResultActionId(CallToolResult result, string tool)
    {
        if (result.IsError == true)
        {
            throw new InvalidOperationException(
                $"{tool} returned an MCP error: {result.StructuredContent?.GetRawText()}");
        }

        var content = result.StructuredContent
            ?? throw new InvalidDataException($"{tool} returned no structured content.");

        return content
            .GetProperty("data")
            .GetProperty("actionId")
            .GetGuid();
    }

    private static void ValidateCapturedReferenceUse(
        string tool,
        CodeActionSelectionDefinition selection)
    {
        if (!string.Equals(tool, "stage-code-action", StringComparison.Ordinal)
            && !string.Equals(tool, "prepare-fix-all", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A captured Code Action reference can only be supplied to prepare-fix-all or stage-code-action.");
        }

        if (selection.TitleContains is not null
            || selection.DiagnosticId is not null
            || selection.Location is not null
            || selection.Arguments.ValueKind != JsonValueKind.Undefined)
        {
            throw new InvalidDataException(
                "A captured Code Action reference selection cannot also define discovery properties.");
        }

        if (selection.CaptureAs is not null
            && !string.Equals(tool, "prepare-fix-all", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only prepare-fix-all can capture a new reference while using a captured originating reference.");
        }
    }

    private static void ValidateDiscoverySelection(
        CodeActionSelectionDefinition selection,
        bool requireCaptureName)
    {
        if (selection.Arguments.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Code Action discovery selection requires list-code-actions arguments.");
        }

        if (selection.TitleContains is null
            && selection.DiagnosticId is null
            && selection.Location is null)
        {
            throw new InvalidDataException(
                "Code Action discovery selection requires a title, diagnostic ID or precise location.");
        }

        if (requireCaptureName && string.IsNullOrWhiteSpace(selection.CaptureAs))
        {
            throw new InvalidDataException(
                "This Code Action workflow step requires a non-empty capture name.");
        }

        if (!requireCaptureName && selection.CaptureAs is not null)
        {
            throw new InvalidDataException(
                "Direct stage-code-action discovery cannot capture its consumed reference.");
        }
    }

    private static string GetRequiredCaptureName(
        CodeActionSelectionDefinition selection)
    {
        return selection.CaptureAs
            ?? throw new InvalidDataException(
                "This Code Action workflow step requires a non-empty capture name.");
    }
}
