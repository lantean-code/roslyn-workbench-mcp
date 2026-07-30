using System.Text.Json;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

internal sealed class CodeActionWorkflowInvoker
{
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
        if (selection is not null)
        {
            if (!string.Equals(tool, "stage-code-action", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "A Code Action selection can only supply an actionId to stage-code-action.");
            }

            arguments["actionId"] = await SelectActionIdAsync(selection, cancellationToken);
        }

        return await _host.CallToolAsync(tool, arguments, cancellationToken);
    }

    private async Task<Guid> SelectActionIdAsync(
        CodeActionSelectionDefinition selection,
        CancellationToken cancellationToken)
    {
        var listResult = await _host.CallToolAsync(
            "list-code-actions",
            Materialize(selection.Arguments),
            cancellationToken);

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
            var availableTitles = items
                .EnumerateArray()
                .Select(static item => item.GetProperty("title").GetString())
                .Where(static title => title is not null);

            throw new InvalidOperationException(
                $"Code Action selection '{selection.TitleContains}' matched {matches.Count} actions. Available titles: {string.Join(", ", availableTitles)}.");
        }

        return matches[0].GetProperty("actionId").GetGuid();
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
        if (title is null
            || !title.Contains(selection.TitleContains, StringComparison.Ordinal))
        {
            return false;
        }

        return selection.DiagnosticId is null
            || HasDiagnostic(item, selection.DiagnosticId);
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
}
