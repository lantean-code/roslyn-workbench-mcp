using System.Text.Json;
using System.Text.Json.Nodes;

using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetProjectDetailsToolTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GIVEN_ProjectStructureServiceFrameworks_WHEN_CallingExecute_THEN_ShouldUseConfiguredTargetFrameworks()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var resolver = workspace.CreateResolver(workspaceIdentity);
        var project = workspace.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();
        var services = new ToolExecutionServicesBuilder()
            .WithProjectStructureService(projectStructureService.Object)
            .Build();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(resolver)
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(services)
            .Build();
        var target = new GetProjectDetailsTool();

        projectStructureService
            .Setup(service => service.GetTargetFrameworks(project))
            .Returns(["net10.0", "net9.0"]);

        var result = await target.ExecuteAsync(new GetProjectDetailsRequest
        {
            Project = new ProjectSelector
            {
                Path = "Sample.csproj",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Project!.TargetFrameworks.Should().Equal("net10.0", "net9.0");
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_RequestingProjectDetailsByDefault_THEN_ShouldOmitDocumentInventory()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<ProjectDetailsData>(executor, registry, "get-project-details", new Dictionary<string, JsonElement>
        {
            ["project"] = JsonSerializer.SerializeToElement(new ProjectSelector
            {
                Path = "Sample.csproj",
            }),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Documents.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_RequestingProjectDocumentsExplicitly_THEN_ShouldReturnBoundedDocumentInventory()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<ProjectDetailsData>(executor, registry, "get-project-details", new Dictionary<string, JsonElement>
        {
            ["project"] = JsonSerializer.SerializeToElement(new ProjectSelector
            {
                Path = "Sample.csproj",
            }),
            ["includeDocuments"] = JsonSerializer.SerializeToElement(true),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Documents.Should().NotBeNull();
        result.Data.Documents.Should().NotBeEmpty();
        result.Data.ReturnedCount.Should().Be(result.Data.Documents.Count);
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
