using System.Text.Json;
using System.Text.Json.Nodes;

using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetControlFlowGraphToolTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_RequestingControlFlowGraph_THEN_ShouldReturnProjectedRegions()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<ControlFlowGraphData>(executor, registry, "get-control-flow-graph", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FlowSamples.AnalyseExceptional(System.String)",
            }),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Regions.Select(static region => region.Kind).Should().Contain(static kind => kind.Contains("Try", StringComparison.Ordinal) || kind.Contains("Catch", StringComparison.Ordinal) || kind.Contains("Finally", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_RequestingBoundedControlFlowGraph_THEN_ShouldRespectRequestedLimits()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var boundedBlocks = await ExecuteAsync<ControlFlowGraphData>(executor, registry, "get-control-flow-graph", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FlowSamples.Analyse(System.String)",
            }),
            ["maxBlocks"] = JsonSerializer.SerializeToElement(1),
        });
        var boundedRegions = await ExecuteAsync<ControlFlowGraphData>(executor, registry, "get-control-flow-graph", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FlowSamples.AnalyseExceptional(System.String)",
            }),
            ["maxRegions"] = JsonSerializer.SerializeToElement(1),
        });

        boundedBlocks.Outcome.Should().Be(ToolOutcome.Succeeded);
        boundedBlocks.Data!.Blocks.Should().HaveCount(1);
        boundedBlocks.Data.BlocksTruncated.Should().BeTrue();
        boundedRegions.Outcome.Should().Be(ToolOutcome.Succeeded);
        boundedRegions.Data!.Regions.Should().HaveCount(1);
        boundedRegions.Data.RegionsTruncated.Should().BeTrue();
    }

    private static async Task<ToolResult<TResponse>> ExecuteAsync<TResponse>(
        ToolExecutor executor,
        PluginRegistry registry,
        string toolName,
        IDictionary<string, JsonElement> arguments)
    {
        var registeredTool = registry.RegisteredTools.Single(tool => tool.Metadata.Name == toolName);
        var result = await executor.ExecuteAsync(registeredTool, arguments, CancellationToken.None);

        result.IsError.Should().BeFalse();

        return DeserializeToolResult<TResponse>(registeredTool, result.StructuredContent!.Value, toolName);
    }

    private static ToolResult<TResponse> DeserializeToolResult<TResponse>(RegisteredTool registeredTool, JsonElement payload, string toolName)
    {
        if (payload.TryGetProperty("outcome", out _))
        {
            return JsonSerializer.Deserialize<ToolResult<TResponse>>(payload.GetRawText(), _serializerOptions)!;
        }

        if (!payload.GetProperty("ok").GetBoolean())
        {
            return ToolResult<TResponse>.Rejected(
                JsonSerializer.Deserialize<ToolError>(payload.GetProperty("error").GetRawText(), _serializerOptions)!,
                payload.TryGetProperty("next", out var nextElement) && nextElement.ValueKind != JsonValueKind.Null
                    ? JsonSerializer.Deserialize<RequiredAction>(nextElement.GetRawText(), _serializerOptions)
                    : null);
        }

        var data = registeredTool.ResponseDescriptor.Kind switch
        {
            ToolResponseShapeKind.Singleton => JsonSerializer.Deserialize<TResponse>(payload.GetProperty("value").GetRawText(), _serializerOptions)!,
            ToolResponseShapeKind.Collection => DeserializeCollectionData<TResponse>(registeredTool.ResponseDescriptor, payload),
            ToolResponseShapeKind.Direct => DeserializeDirectData<TResponse>(payload),
            ToolResponseShapeKind.Mutation => throw new InvalidOperationException("Mutation shape is not expected in these tests."),
            ToolResponseShapeKind.CodeActionList => throw new InvalidOperationException("Code action list shape is not expected in these tests."),
            _ => throw new InvalidOperationException($"Unsupported response shape kind '{registeredTool.ResponseDescriptor.Kind}'."),
        };

        return ToolResult<TResponse>.Succeeded(data);
    }

    private static TResponse DeserializeDirectData<TResponse>(JsonElement payload)
    {
        var node = JsonNode.Parse(payload.GetRawText())!.AsObject();
        node.Remove("ok");

        return node.Deserialize<TResponse>(_serializerOptions)!;
    }

    private static TResponse DeserializeCollectionData<TResponse>(ToolResponseDescriptor descriptor, JsonElement payload)
    {
        var node = JsonNode.Parse(payload.GetRawText())!.AsObject();
        var itemsNode = node["items"]?.DeepClone();
        var hasMoreNode = node["hasMore"]?.DeepClone();
        var truncatedByNode = node["truncatedBy"]?.DeepClone();

        node.Remove("ok");
        node.Remove("items");
        node.Remove("hasMore");
        node.Remove("truncatedBy");
        node[JsonNamingPolicy.CamelCase.ConvertName(descriptor.CollectionPropertyName!)] = itemsNode;
        node["hasMore"] = hasMoreNode;
        node["returnedCount"] = itemsNode is JsonArray itemsArray ? itemsArray.Count : 0;

        if (truncatedByNode is not null)
        {
            node["truncationReasons"] = truncatedByNode;
        }

        return node.Deserialize<TResponse>(_serializerOptions)!;
    }
}
