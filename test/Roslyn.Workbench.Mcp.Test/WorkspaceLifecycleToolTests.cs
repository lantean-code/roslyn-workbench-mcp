using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Moq;

using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Contracts.Transactions;

using Roslyn.Workbench.Mcp.Workspace;
using Roslyn.Workbench.Mcp.Workspace.Test;

using Xunit;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class WorkspaceLifecycleToolTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GIVEN_LifecycleTools_WHEN_OpeningAndReadingStatus_THEN_ShouldReturnStructuredWorkspaceResults()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
        });
        var tools = WorkspaceLifecycleToolFactory.Create(coordinator);
        var openTool = tools.Single(tool => tool.ProtocolTool.Name == "workspace-open");
        var statusTool = tools.Single(tool => tool.ProtocolTool.Name == "workspace-status");

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

        var openPayload = JsonSerializer.Deserialize<ToolResult<WorkspaceOpenData>>(openResult.StructuredContent!.Value.GetRawText(), _serializerOptions);

        openResult.IsError.Should().BeFalse();
        openPayload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        openPayload.Data!.Workspace!.LoadedPath.Should().Be(fixture.ProjectPath);

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

        var statusPayload = JsonSerializer.Deserialize<ToolResult<WorkspaceStatusData>>(statusResult.StructuredContent!.Value.GetRawText(), _serializerOptions);

        statusResult.IsError.Should().BeFalse();
        statusPayload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        statusPayload.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        statusPayload.WorkspaceEpoch.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_UnloadedCoordinator_WHEN_InvokingWorkspaceCloseTool_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions());
        var tool = WorkspaceLifecycleToolFactory.Create(coordinator).Single(static value => value.ProtocolTool.Name == "workspace-close");

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

        var payload = JsonSerializer.Deserialize<ToolResult<WorkspaceCloseData>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        result.IsError.Should().BeTrue();
        payload!.Outcome.Should().Be(ToolOutcome.Rejected);
        payload.Error!.Code.Should().Be("WorkspaceNotOpen");
    }

    [Fact]
    public async Task GIVEN_ReadyWorkspace_WHEN_InvokingWorkspaceReloadTool_THEN_ShouldReturnWorkspaceReloadNotRequired()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions());
        var tools = WorkspaceLifecycleToolFactory.Create(coordinator);
        var openTool = tools.Single(static value => value.ProtocolTool.Name == "workspace-open");
        var reloadTool = tools.Single(static value => value.ProtocolTool.Name == "workspace-reload");

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

        var payload = JsonSerializer.Deserialize<ToolResult<WorkspaceReloadData>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        result.IsError.Should().BeTrue();
        payload!.Outcome.Should().Be(ToolOutcome.Rejected);
        payload.Error!.Code.Should().Be("WorkspaceReloadNotRequired");
    }

    [Fact]
    public async Task GIVEN_OpenedWorkspace_WHEN_InvokingTransactionLifecycleTools_THEN_ShouldReturnStructuredTransactionResults()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions());
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var tools = TransactionToolFactory.Create(coordinator);
        var startTool = tools.Single(static value => value.ProtocolTool.Name == "transaction-start");
        var previewTool = tools.Single(static value => value.ProtocolTool.Name == "transaction-preview");
        var rollbackTool = tools.Single(static value => value.ProtocolTool.Name == "transaction-rollback");

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
        var startPayload = JsonSerializer.Deserialize<ToolResult<TransactionStartData>>(startResult.StructuredContent!.Value.GetRawText(), _serializerOptions);

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
        var previewPayload = JsonSerializer.Deserialize<ToolResult<TransactionPreviewData>>(previewResult.StructuredContent!.Value.GetRawText(), _serializerOptions);

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
        var rollbackPayload = JsonSerializer.Deserialize<ToolResult<TransactionRollbackData>>(rollbackResult.StructuredContent!.Value.GetRawText(), _serializerOptions);

        startPayload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        startPayload.Data!.Transaction!.Revision.Should().Be(0);
        previewPayload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        previewPayload.Data!.Transaction!.Revision.Should().Be(0);
        rollbackPayload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        rollbackPayload.Data!.State.Should().Be(TransactionRollbackState.Ready);
    }

    [Fact]
    public void GIVEN_LifecycleToolFactory_WHEN_CreatingTools_THEN_ShouldPublishExpectedToolNames()
    {
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions());

        var tools = WorkspaceLifecycleToolFactory.Create(coordinator);

        tools.Select(static tool => tool.ProtocolTool.Name).Should().Contain(
        [
            "workspace-open",
            "workspace-close",
            "workspace-status",
            "workspace-reload",
        ]);
    }

    [Fact]
    public void GIVEN_TransactionToolFactory_WHEN_CreatingTools_THEN_ShouldPublishExpectedToolNames()
    {
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions());

        var tools = TransactionToolFactory.Create(coordinator);

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
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions());

        var tool = TransactionToolFactory.Create(coordinator).Single(static value => value.ProtocolTool.Name == "transaction-history");

        tool.ProtocolTool.Annotations.Should().NotBeNull();
        tool.ProtocolTool.Annotations!.ReadOnlyHint.Should().BeFalse();
        tool.ProtocolTool.Annotations.DestructiveHint.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_ThrowingLifecycleHandler_WHEN_InvokingServerTool_THEN_ShouldReturnStructuredFaultResult()
    {
        var tool = new ServerToolMcpServerTool<EmptyRequest, WorkspaceStatusData>(
            "test-status",
            "Test Status",
            "Throws for testing.",
            readOnly: true,
            destructive: false,
            ToolOutputSchemaMode.Omit,
            resultSummary: null,
            (_, _, _) => throw new InvalidOperationException("Boom"));

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

        var payload = JsonSerializer.Deserialize<ToolResult<WorkspaceStatusData>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        result.IsError.Should().BeTrue();
        payload!.Outcome.Should().Be(ToolOutcome.Faulted);
        payload.Error!.Code.Should().Be("UnhandledException");
    }

    [Fact]
    public async Task GIVEN_InvalidLifecycleRequestPayload_WHEN_InvokingServerTool_THEN_ShouldReturnStructuredFaultResult()
    {
        var tool = new ServerToolMcpServerTool<WorkspaceOpenRequest, WorkspaceOpenData>(
            "workspace-open",
            "Workspace Open",
            "Deserializes request for testing.",
            readOnly: false,
            destructive: false,
            ToolOutputSchemaMode.Omit,
            resultSummary: null,
            (_, _, _) => ValueTask.FromResult(ToolResult<WorkspaceOpenData>.Rejected(new ToolError
            {
                Code = "Unreachable",
                Message = "Unreachable.",
            })));

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

        var payload = JsonSerializer.Deserialize<ToolResult<WorkspaceOpenData>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        result.IsError.Should().BeTrue();
        payload!.Outcome.Should().Be(ToolOutcome.Faulted);
        payload.Error!.Code.Should().Be("UnhandledException");
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
