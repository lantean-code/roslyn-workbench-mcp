using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class InspectionMcpToolTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_InvokingStage4ToolsThroughMcpAdapter_THEN_ShouldReturnStructuredResults()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);
        var resolveLocation = fixture.GetLocation("GreetingFormatter");

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var solutionStructure = await InvokeAsync<SolutionStructureData>(executor, registry, "get-solution-structure", new Dictionary<string, JsonElement>());
        var projectDetails = await InvokeAsync<ProjectDetailsData>(executor, registry, "get-project-details", new Dictionary<string, JsonElement>
        {
            ["project"] = JsonSerializer.SerializeToElement(new ProjectSelector
            {
                Path = "Sample.csproj",
            }),
        });
        var documentOptions = await InvokeAsync<DocumentOptionsData>(executor, registry, "get-document-options", new Dictionary<string, JsonElement>
        {
            ["document"] = JsonSerializer.SerializeToElement(new DocumentSelector
            {
                Path = "Formatting.cs",
            }),
        });
        var searchSymbols = await InvokeAsync<SymbolSearchData>(executor, registry, "search-symbols", new Dictionary<string, JsonElement>
        {
            ["query"] = JsonSerializer.SerializeToElement("Greeting"),
        });
        var resolveSymbol = await InvokeAsync<ResolveSymbolData>(executor, registry, "resolve-symbol", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(resolveLocation),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var symbolInfo = await InvokeAsync<SymbolInfoData>(executor, registry, "get-symbol-info", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(resolveSymbol.Data!.Selector),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var definition = await InvokeAsync<DefinitionData>(executor, registry, "go-to-definition", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(resolveSymbol.Data!.Selector),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var references = await InvokeAsync<ReferenceSearchData>(executor, registry, "find-references", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(resolveSymbol.Data!.Selector),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var callers = await InvokeAsync<CallerSearchData>(executor, registry, "find-callers", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            }),
        });
        var implementations = await InvokeAsync<ImplementationSearchData>(executor, registry, "find-implementations", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.IMessageFormatter",
            }),
        });
        var diagnostics = await InvokeAsync<DiagnosticsData>(executor, registry, "get-diagnostics", new Dictionary<string, JsonElement>());
        var outline = await InvokeAsync<DocumentOutlineData>(executor, registry, "get-document-outline", new Dictionary<string, JsonElement>
        {
            ["document"] = JsonSerializer.SerializeToElement(new DocumentSelector
            {
                Path = "Formatting.cs",
            }),
        });

        solutionStructure.Data!.Projects.Should().ContainSingle(static project => project.Name == "Sample");
        projectDetails.Data!.Project!.Name.Should().Be("Sample");
        documentOptions.Data!.AnalyzerConfig!.EditorConfigPaths.Should().Contain(static path => path.EndsWith(".editorconfig", StringComparison.Ordinal));
        documentOptions.Data.AnalyzerConfig.Options.Should().ContainKey("build_property.targetframework");
        searchSymbols.Data!.Symbols.Should().Contain(static symbol => symbol.DisplayName.Contains("GreetingFormatter", StringComparison.Ordinal));
        resolveSymbol.Data!.Symbol!.DisplayName.Should().Contain("GreetingFormatter");
        symbolInfo.Data!.Symbol!.DisplayName.Should().Contain("GreetingFormatter");
        definition.Data!.Definitions.Should().NotBeEmpty();
        references.Data!.References.Should().NotBeEmpty();
        callers.Data!.Callers.Should().Contain(static caller => caller.Caller!.DisplayName.Contains("Call", StringComparison.Ordinal));
        implementations.Data!.Implementations.Should().Contain(static symbol => symbol.DisplayName.Contains("GreetingFormatter", StringComparison.Ordinal));
        diagnostics.Data!.Diagnostics.Should().Contain(static diagnostic => diagnostic.Id == "CS0219");
        EnumerateOutline(outline.Data!.Root!).Should().Contain(static node => node.Name == "GreetingFormatter");
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_InvokingBatch1ToolsThroughMcpAdapter_THEN_ShouldReturnStructuredResults()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var typeHierarchy = await InvokeAsync<TypeHierarchyData>(executor, registry, "get-type-hierarchy", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.GreetingFormatter",
            }),
            ["includeDerived"] = JsonSerializer.SerializeToElement(true),
        });
        var startResult = await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var renameResult = await InvokeAsync<MutationData>(executor, registry, "rename-symbol", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.StateHolder",
            }),
            ["newName"] = JsonSerializer.SerializeToElement("SessionState"),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = startResult.Data!.Transaction!.Revision,
            }),
        });

        typeHierarchy.Data!.Interfaces.Should().Contain(static symbol => symbol.DisplayName.Contains("IMessageFormatter", StringComparison.Ordinal));
        renameResult.Data!.Operation.Should().Be("rename-symbol");
    }

    private static async Task<ToolResult<TResponse>> InvokeAsync<TResponse>(
        ToolExecutor executor,
        PluginRegistry registry,
        string toolName,
        IDictionary<string, JsonElement> arguments)
    {
        var registeredTool = registry.RegisteredTools.Single(tool => tool.Metadata.Name == toolName);
        var serverTool = new PluginMcpServerTool(registeredTool, executor);
        var result = await serverTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = toolName,
                    Arguments = arguments,
                }),
            CancellationToken.None);

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
            ToolResponseShapeKind.Direct => DeserializeDirectData<TResponse>(payload),
            ToolResponseShapeKind.Singleton => JsonSerializer.Deserialize<TResponse>(payload.GetProperty("value").GetRawText(), _serializerOptions)!,
            ToolResponseShapeKind.Collection => DeserializeCollectionData<TResponse>(registeredTool.ResponseDescriptor, payload),
            ToolResponseShapeKind.Mutation => (TResponse)(object)DeserializeMutationData(payload, toolName),
            ToolResponseShapeKind.CodeActionList => (TResponse)(object)DeserializeCodeActionListData(payload),
            _ => throw new InvalidOperationException($"Unsupported response shape kind '{registeredTool.ResponseDescriptor.Kind}'."),
        };

        var transactionRevision = data is MutationData mutationData
            ? mutationData.Transaction?.Revision
            : null;

        return ToolResult<TResponse>.Succeeded(data, transactionRevision: transactionRevision);
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

    private static MutationData DeserializeMutationData(JsonElement payload, string toolName)
    {
        return new MutationData
        {
            Operation = toolName,
            Summary = payload.TryGetProperty("summary", out var summaryElement) && summaryElement.ValueKind == JsonValueKind.String
                ? summaryElement.GetString() ?? string.Empty
                : string.Empty,
            Transaction = payload.TryGetProperty("transaction", out var transactionElement)
                ? new TransactionInfo
                {
                    Revision = transactionElement.GetProperty("revision").GetInt32(),
                }
                : null,
        };
    }

    private static CodeActionListData DeserializeCodeActionListData(JsonElement payload)
    {
        var items = JsonSerializer.Deserialize<IReadOnlyList<CodeActionListItem>>(payload.GetProperty("items").GetRawText(), _serializerOptions) ?? [];

        return new CodeActionListData
        {
            Actions = items.Select(static item => new CodeActionInfo
            {
                ActionId = item.ActionId,
                Title = item.Title,
                ProviderId = item.ProviderId,
                Kind = item.Kind,
                ExecutionMode = item.ExecutionMode,
                ExecutorTool = item.ExecutorTool,
                DescribeTool = item.DescribeTool,
                UnsupportedReasonCode = item.UnsupportedReasonCode,
            }).ToArray(),
            ReturnedCount = items.Count,
            HasMore = payload.GetProperty("hasMore").GetBoolean(),
            TruncationReasons = payload.TryGetProperty("truncatedBy", out var truncatedByElement)
                ? JsonSerializer.Deserialize<IReadOnlyList<CollectionTruncation>>(truncatedByElement.GetRawText(), _serializerOptions)
                : null,
        };
    }

    private static IEnumerable<OutlineNode> EnumerateOutline(OutlineNode root)
    {
        yield return root;

        foreach (var child in root.Children)
        {
            foreach (var descendant in EnumerateOutline(child))
            {
                yield return descendant;
            }
        }
    }

    private static McpServer CreateServer()
    {
        var asyncDisposable = new Mock<IAsyncDisposable>();
        var server = new Mock<McpServer>();

        asyncDisposable.Setup(static disposable => disposable.DisposeAsync()).Returns(ValueTask.CompletedTask);
        server.SetupGet(static value => value.ClientCapabilities).Returns(new ClientCapabilities());
        server.SetupGet(static value => value.ClientInfo).Returns(new Implementation
        {
            Name = "Test Client",
            Version = "1.0.0",
        });
        server.SetupGet(static value => value.ServerOptions).Returns(new McpServerOptions());
        server.SetupGet(static value => value.Services).Returns(Mock.Of<IServiceProvider>());
        server.SetupGet(static value => value.LoggingLevel).Returns((LoggingLevel?)null);
        server.SetupGet(static value => value.SessionId).Returns("session");
        server.SetupGet(static value => value.NegotiatedProtocolVersion).Returns("2025-06-18");
        server.Setup(static value => value.RunAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        server
            .Setup(static value => value.SendRequestAsync(It.IsAny<JsonRpcRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonRpcResponse
            {
                Result = new JsonObject(),
            });
        server
            .Setup(static value => value.SendMessageAsync(It.IsAny<JsonRpcMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        server
            .Setup(static value => value.RegisterNotificationHandler(It.IsAny<string>(), It.IsAny<Func<JsonRpcNotification, CancellationToken, ValueTask>>()))
            .Returns(asyncDisposable.Object);
        server.Setup(static value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);

        return server.Object;
    }
}
