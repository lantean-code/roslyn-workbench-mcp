using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Contracts.Transactions;
using Roslyn.Workbench.Mcp.TestSupport;
using Roslyn.Workbench.Mcp.Tools;
using Roslyn.Workbench.Mcp.Workspace.Test;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class WorkspaceLifecycleToolTests
{
    [Fact]
    public async Task GIVEN_LifecycleTools_WHEN_OpeningAndReadingStatus_THEN_ShouldReturnStructuredWorkspaceResults()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var runtime = WorkspaceCoordinatorFactory.Create();
        var openTool = new WorkspaceOpenTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);
        var statusTool = new WorkspaceStatusTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);

        var openResult = await openTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-open",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["path"] = JsonSerializer.SerializeToElement(fixture.ProjectPath),
                    },
                }),
            CancellationToken.None);

        openResult.IsError.Should().BeFalse();
        openResult.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        openResult.StructuredContent.Value.GetProperty("workspace").GetProperty("loadedPath").GetString().Should().Be(fixture.ProjectPath);

        var statusResult = await statusTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-status",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);

        statusResult.IsError.Should().BeFalse();
        statusResult.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        statusResult.StructuredContent.Value.GetProperty("state").GetString().Should().Be(nameof(WorkspaceLifecycleState.Ready));
        statusResult.StructuredContent.Value.GetProperty("workspace").GetProperty("workspaceEpoch").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_TwoOpenedWorkspaces_WHEN_ListingAndReadingStatusWithoutSelection_THEN_ShouldRequireExplicitWorkspaceSelection()
    {
        using var fixtureA = await TestWorkspaceFixture.CreateAsync();
        using var fixtureB = await TestWorkspaceFixture.CreateAsync();
        var runtime = WorkspaceCoordinatorFactory.Create();
        var openTool = new WorkspaceOpenTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);
        var listTool = new WorkspaceListTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);
        var statusTool = new WorkspaceStatusTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);

        var openA = await openTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-open",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["alias"] = JsonSerializer.SerializeToElement("alpha"),
                        ["path"] = JsonSerializer.SerializeToElement(fixtureA.ProjectPath),
                    },
                }),
            CancellationToken.None);
        var openAWorkspaceId = openA.StructuredContent!.Value.GetProperty("workspace").GetProperty("workspaceId").GetString();

        await openTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-open",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["alias"] = JsonSerializer.SerializeToElement("beta"),
                        ["path"] = JsonSerializer.SerializeToElement(fixtureB.ProjectPath),
                    },
                }),
            CancellationToken.None);

        var listResult = await listTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-list",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);
        var ambiguousStatusResult = await statusTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-status",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);
        var selectedStatusResult = await statusTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-status",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["workspace"] = JsonSerializer.SerializeToElement(new
                        {
                            workspaceId = openAWorkspaceId,
                        }),
                    },
                }),
            CancellationToken.None);

        listResult.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        listResult.StructuredContent.Value.GetProperty("workspaces").GetArrayLength().Should().Be(2);
        ambiguousStatusResult.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        ambiguousStatusResult.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("WorkspaceSelectorRequired");
        selectedStatusResult.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        selectedStatusResult.StructuredContent.Value.GetProperty("workspace").GetProperty("workspaceId").GetString().Should().Be(openAWorkspaceId);
    }

    [Fact]
    public async Task GIVEN_UnloadedCoordinator_WHEN_InvokingWorkspaceCloseTool_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var runtime = WorkspaceCoordinatorFactory.Create();
        var tool = new WorkspaceCloseTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);

        var result = await tool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-close",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("WorkspaceNotOpen");
    }

    [Fact]
    public async Task GIVEN_ReadyWorkspace_WHEN_InvokingWorkspaceReloadTool_THEN_ShouldReturnWorkspaceReloadNotRequired()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var runtime = WorkspaceCoordinatorFactory.Create();
        var openTool = new WorkspaceOpenTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);
        var reloadTool = new WorkspaceReloadTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);

        await openTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-open",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["path"] = JsonSerializer.SerializeToElement(fixture.ProjectPath),
                    },
                }),
            CancellationToken.None);

        var result = await reloadTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-reload",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("WorkspaceReloadNotRequired");
    }

    [Fact]
    public async Task GIVEN_OpenedWorkspace_WHEN_InvokingTransactionLifecycleTools_THEN_ShouldReturnStructuredTransactionResults()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var runtime = WorkspaceCoordinatorFactory.Create();
        await runtime.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var startTool = new TransactionStartTool(CreateStartupOptions(), runtime.TransactionService);
        var previewTool = new TransactionPreviewTool(CreateStartupOptions(), runtime.TransactionService);
        var rollbackTool = new TransactionRollbackTool(CreateStartupOptions(), runtime.TransactionService);

        var startResult = await startTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "transaction-start",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);
        var previewResult = await previewTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "transaction-preview",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);
        var rollbackResult = await rollbackTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "transaction-rollback",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);
        startResult.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        startResult.StructuredContent.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(0);
        previewResult.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        previewResult.StructuredContent.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(0);
        rollbackResult.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        rollbackResult.StructuredContent.Value.GetProperty("state").GetString().Should().Be(nameof(TransactionRollbackState.Ready));
    }

    [Fact]
    public void GIVEN_ServerOwnedWorkspaceTools_WHEN_CreatingTools_THEN_ShouldPublishExpectedToolNames()
    {
        var runtime = WorkspaceCoordinatorFactory.Create();
        var tools = new McpServerTool[]
        {
            new WorkspaceOpenTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService),
            new WorkspaceListTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService),
            new WorkspaceCloseTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService),
            new WorkspaceStatusTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService),
            new WorkspaceReloadTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService),
        };

        tools.Select(static tool => tool.ProtocolTool.Name).Should().Contain(
        [
            "workspace-open",
            "workspace-list",
            "workspace-close",
            "workspace-status",
            "workspace-reload",
        ]);
    }

    [Fact]
    public void GIVEN_ServerOwnedTransactionTools_WHEN_CreatingTools_THEN_ShouldPublishExpectedToolNames()
    {
        var runtime = WorkspaceCoordinatorFactory.Create();
        var tools = new McpServerTool[]
        {
            new TransactionStartTool(CreateStartupOptions(), runtime.TransactionService),
            new TransactionPreviewTool(CreateStartupOptions(), runtime.TransactionService),
            new TransactionHistoryTool(CreateStartupOptions(), runtime.TransactionService),
            new TransactionCommitTool(CreateStartupOptions(), runtime.TransactionService),
            new TransactionRollbackTool(CreateStartupOptions(), runtime.TransactionService),
        };

        tools.Select(static tool => tool.ProtocolTool.Name).Should().Contain(
        [
            "transaction-start",
            "transaction-preview",
            "transaction-history",
            "transaction-commit",
            "transaction-rollback",
        ]);
    }

    [Fact]
    public void GIVEN_TransactionHistoryTool_WHEN_CreatingServerTool_THEN_ShouldPublishDestructiveAnnotation()
    {
        var runtime = WorkspaceCoordinatorFactory.Create();
        var tool = new TransactionHistoryTool(CreateStartupOptions(), runtime.TransactionService);

        tool.ProtocolTool.Annotations.Should().NotBeNull();
        tool.ProtocolTool.Annotations!.ReadOnlyHint.Should().BeFalse();
        tool.ProtocolTool.Annotations.DestructiveHint.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_ThrowingLifecycleHandler_WHEN_InvokingServerTool_THEN_ShouldReturnStructuredFaultResult()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        service
            .Setup(static value => value.GetStatusAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<StatusDetailLevel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Boom"));
        var tool = new WorkspaceStatusTool(CreateStartupOptions(), service.Object);

        var result = await tool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "test-status",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("UnhandledException");
    }

    [Fact]
    public async Task GIVEN_InvalidLifecycleRequestPayload_WHEN_InvokingServerTool_THEN_ShouldReturnStructuredFaultResult()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        var tool = new WorkspaceOpenTool(CreateStartupOptions(), service.Object);

        var result = await tool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-open",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["path"] = JsonSerializer.SerializeToElement(new { value = "Path" }),
                    },
                }),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("UnhandledException");
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

    private static IOptions<StartupOptions> CreateStartupOptions()
    {
        return Options.Create(new StartupOptions());
    }
}
