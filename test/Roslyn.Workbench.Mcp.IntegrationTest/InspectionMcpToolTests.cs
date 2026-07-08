using System.Text.Json;
using System.Text.Json.Nodes;
using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.Test;

[Trait("Category", "Integration")]
public sealed class InspectionMcpToolTests
{
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
        var resolveLocation = fixture.GetLocation("GreetingFormatter");

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var solutionStructure = await InvokeAsync<SolutionStructureData>(coordinator, registry, "get-solution-structure", new Dictionary<string, JsonElement>());
        var projectDetails = await InvokeAsync<ProjectDetailsData>(coordinator, registry, "get-project-details", new Dictionary<string, JsonElement>
        {
            ["project"] = JsonSerializer.SerializeToElement(new ProjectSelector
            {
                Path = "Sample.csproj",
            }),
        });
        var documentOptions = await InvokeAsync<DocumentOptionsData>(coordinator, registry, "get-document-options", new Dictionary<string, JsonElement>
        {
            ["document"] = JsonSerializer.SerializeToElement(new DocumentSelector
            {
                Path = "Formatting.cs",
            }),
        });
        var searchSymbols = await InvokeAsync<SymbolSearchData>(coordinator, registry, "search-symbols", new Dictionary<string, JsonElement>
        {
            ["query"] = JsonSerializer.SerializeToElement("Greeting"),
        });
        var resolveSymbol = await InvokeAsync<ResolveSymbolData>(coordinator, registry, "resolve-symbol", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(resolveLocation),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var symbolInfo = await InvokeAsync<SymbolInfoData>(coordinator, registry, "get-symbol-info", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(resolveSymbol.Data!.Selector),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var definition = await InvokeAsync<DefinitionData>(coordinator, registry, "go-to-definition", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(resolveSymbol.Data!.Selector),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var references = await InvokeAsync<ReferenceSearchData>(coordinator, registry, "find-references", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(resolveSymbol.Data!.Selector),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var callers = await InvokeAsync<CallerSearchData>(coordinator, registry, "find-callers", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            }),
        });
        var implementations = await InvokeAsync<ImplementationSearchData>(coordinator, registry, "find-implementations", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.IMessageFormatter",
            }),
        });
        var diagnostics = await InvokeAsync<DiagnosticsData>(coordinator, registry, "get-diagnostics", new Dictionary<string, JsonElement>());
        var outline = await InvokeAsync<DocumentOutlineData>(coordinator, registry, "get-document-outline", new Dictionary<string, JsonElement>
        {
            ["document"] = JsonSerializer.SerializeToElement(new DocumentSelector
            {
                Path = "Formatting.cs",
            }),
        });

        solutionStructure.Data!.Projects.Items.Should().ContainSingle(static project => project.Name == "Sample");
        projectDetails.Data!.Project!.Name.Should().Be("Sample");
        documentOptions.Data!.AnalyzerConfig!.EditorConfigPaths.Should().Contain(static path => path.EndsWith(".editorconfig", StringComparison.Ordinal));
        documentOptions.Data.AnalyzerConfig.Options.Should().ContainKey("build_property.targetframework");
        searchSymbols.Data!.Symbols.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("GreetingFormatter", StringComparison.Ordinal));
        resolveSymbol.Data!.Symbol!.DisplayName.Should().Contain("GreetingFormatter");
        symbolInfo.Data!.Symbol!.DisplayName.Should().Contain("GreetingFormatter");
        definition.Data!.Definitions.Should().NotBeEmpty();
        references.Data!.References.Items.Should().NotBeEmpty();
        callers.Data!.Callers.Items.Should().Contain(static caller => caller.Caller!.DisplayName.Contains("Call", StringComparison.Ordinal));
        implementations.Data!.Implementations.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("GreetingFormatter", StringComparison.Ordinal));
        diagnostics.Data!.Diagnostics.Items.Should().Contain(static diagnostic => diagnostic.Id == "CS0219");
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
        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var typeHierarchy = await InvokeAsync<TypeHierarchyData>(coordinator, registry, "get-type-hierarchy", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.GreetingFormatter",
            }),
            ["includeDerived"] = JsonSerializer.SerializeToElement(true),
        });
        var startResult = await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var renameResult = await InvokeAsync<MutationData>(coordinator, registry, "rename-symbol", new Dictionary<string, JsonElement>
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

        typeHierarchy.Data!.Interfaces.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("IMessageFormatter", StringComparison.Ordinal));
        renameResult.Data!.Operation.Should().Be("rename-symbol");
    }

    private static async Task<ToolResult<TResponse>> InvokeAsync<TResponse>(
        IToolExecutionContextFactory contextFactory,
        PluginRegistry registry,
        string toolName,
        IDictionary<string, JsonElement> arguments)
    {
        var registeredTool = registry.GetRegisteredPluginTool(toolName);
        var serverTool = new PluginMcpServerTool(registeredTool, contextFactory);
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

        return PluginToolTestHarness.DeserializeToolResult<TResponse>(registeredTool.Tool, result.StructuredContent!.Value, toolName);
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
