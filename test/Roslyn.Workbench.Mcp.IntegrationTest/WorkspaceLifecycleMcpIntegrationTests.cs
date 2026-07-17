using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class WorkspaceLifecycleMcpIntegrationTests
{
    [Fact]
    public async Task GIVEN_WorkspaceLifecycleTools_WHEN_OpeningListingReadingReloadingAndClosing_THEN_ShouldReturnStructuredResults()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var runtime = WorkspaceCoordinatorFactory.Create();
        var startupOptions = CreateStartupOptions();
        var protocolFactory = McpIntegrationTestHost.CreateProtocolFactory();
        var openTool = new WorkspaceOpenTool(startupOptions, protocolFactory, runtime.WorkspaceLifecycleService);
        var listTool = new WorkspaceListTool(startupOptions, protocolFactory, runtime.WorkspaceLifecycleService);
        var statusTool = new WorkspaceStatusTool(startupOptions, protocolFactory, runtime.WorkspaceLifecycleService);
        var reloadTool = new WorkspaceReloadTool(startupOptions, protocolFactory, runtime.WorkspaceLifecycleService);
        var closeTool = new WorkspaceCloseTool(startupOptions, protocolFactory, runtime.WorkspaceLifecycleService);

        var open = await McpIntegrationTestHost.InvokeServerToolAsync(openTool, TestContext.Current.CancellationToken, "workspace-open", new Dictionary<string, JsonElement>
        {
            ["path"] = JsonSerializer.SerializeToElement(fixture.ProjectPath),
        });
        var list = await McpIntegrationTestHost.InvokeServerToolAsync(listTool, TestContext.Current.CancellationToken, "workspace-list", new Dictionary<string, JsonElement>());
        var status = await McpIntegrationTestHost.InvokeServerToolAsync(statusTool, TestContext.Current.CancellationToken, "workspace-status", new Dictionary<string, JsonElement>
        {
            ["detail"] = JsonSerializer.SerializeToElement(StatusDetailLevel.Full),
        });
        var reload = await McpIntegrationTestHost.InvokeServerToolAsync(reloadTool, TestContext.Current.CancellationToken, "workspace-reload", new Dictionary<string, JsonElement>());
        var close = await McpIntegrationTestHost.InvokeServerToolAsync(closeTool, TestContext.Current.CancellationToken, "workspace-close", new Dictionary<string, JsonElement>());

        open.IsError.Should().BeFalse();
        open.StructuredContent!.Value.GetProperty("data").GetProperty("workspace").GetProperty("loadedPath").GetString().Should().Be(fixture.ProjectPath);
        list.IsError.Should().BeFalse();
        list.StructuredContent!.Value.GetProperty("data").GetProperty("workspaces").GetArrayLength().Should().Be(1);
        status.IsError.Should().BeFalse();
        var statusData = status.StructuredContent!.Value.GetProperty("data");
        statusData.GetProperty("state").GetString().Should().Be(nameof(WorkspaceLifecycleState.Ready));
        statusData.GetProperty("loadDiagnostics").ValueKind.Should().Be(JsonValueKind.Array);
        reload.IsError.Should().BeTrue();
        reload.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("WorkspaceReloadNotRequired");
        close.IsError.Should().BeFalse();
        close.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_OpenedWorkspace_WHEN_InvokingMutationAndTransactionToolsThroughMcp_THEN_ShouldCompleteTransactionWorkflows()
    {
        await using var fixture = await InspectionSampleFixture.CreateAsync();
        await using var runtime = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var open = await runtime.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);
        var startupOptions = CreateStartupOptions();
        var protocolFactory = McpIntegrationTestHost.CreateProtocolFactory();
        var startTool = new TransactionStartTool(startupOptions, protocolFactory, runtime.TransactionService);
        var previewTool = new TransactionPreviewTool(startupOptions, protocolFactory, runtime.TransactionService);
        var historyTool = new TransactionHistoryTool(startupOptions, protocolFactory, runtime.TransactionService);
        var commitTool = new TransactionCommitTool(startupOptions, protocolFactory, runtime.TransactionService);
        var rollbackTool = new TransactionRollbackTool(startupOptions, protocolFactory, runtime.TransactionService);
        var registry = BundledPluginCatalogueFactory.CreateCatalogue();

        var start = await McpIntegrationTestHost.InvokeServerToolAsync(startTool, TestContext.Current.CancellationToken, "transaction-start", new Dictionary<string, JsonElement>());
        var rename = await McpIntegrationTestHost.InvokePluginToolAsync<MutationData>(runtime, TestContext.Current.CancellationToken, registry, "rename-symbol", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.StateHolder",
            }),
            ["newName"] = JsonSerializer.SerializeToElement("SessionState"),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var preview = await McpIntegrationTestHost.InvokeServerToolAsync(previewTool, TestContext.Current.CancellationToken, "transaction-preview", new Dictionary<string, JsonElement>());
        var undo = await McpIntegrationTestHost.InvokeServerToolAsync(historyTool, TestContext.Current.CancellationToken, "transaction-history", new Dictionary<string, JsonElement>
        {
            ["direction"] = JsonSerializer.SerializeToElement(TransactionHistoryDirection.Undo),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 1,
            }),
        });
        var redo = await McpIntegrationTestHost.InvokeServerToolAsync(historyTool, TestContext.Current.CancellationToken, "transaction-history", new Dictionary<string, JsonElement>
        {
            ["direction"] = JsonSerializer.SerializeToElement(TransactionHistoryDirection.Redo),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var commit = await McpIntegrationTestHost.InvokeServerToolAsync(commitTool, TestContext.Current.CancellationToken, "transaction-commit", new Dictionary<string, JsonElement>
        {
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 1,
            }),
        });
        var secondStart = await McpIntegrationTestHost.InvokeServerToolAsync(startTool, TestContext.Current.CancellationToken, "transaction-start", new Dictionary<string, JsonElement>());
        var rollback = await McpIntegrationTestHost.InvokeServerToolAsync(rollbackTool, TestContext.Current.CancellationToken, "transaction-rollback", new Dictionary<string, JsonElement>());

        start.IsError.Should().BeFalse();
        start.StructuredContent!.Value.GetProperty("data").GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(0);
        rename.Outcome.Should().Be(ToolOutcome.Succeeded);
        rename.Data!.Transaction!.Revision.Should().Be(1);
        preview.IsError.Should().BeFalse();
        preview.StructuredContent!.Value.GetProperty("data").GetProperty("documents").GetArrayLength().Should().Be(1);
        undo.IsError.Should().BeFalse();
        undo.StructuredContent!.Value.GetProperty("data").GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(0);
        redo.IsError.Should().BeFalse();
        redo.StructuredContent!.Value.GetProperty("data").GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(1);
        commit.IsError.Should().BeFalse();
        commit.StructuredContent!.Value.GetProperty("data").GetProperty("committed").GetBoolean().Should().BeTrue();
        secondStart.IsError.Should().BeFalse();
        rollback.IsError.Should().BeFalse();
        rollback.StructuredContent!.Value.GetProperty("data").GetProperty("state").GetString().Should().Be(nameof(TransactionRollbackState.Ready));
    }

    [Fact]
    public async Task GIVEN_InvalidLifecycleArguments_WHEN_InvokingThroughMcp_THEN_ShouldReturnStructuredBindingError()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        var tool = new WorkspaceOpenTool(
            CreateStartupOptions(),
            McpIntegrationTestHost.CreateProtocolFactory(),
            service.Object);

        var result = await McpIntegrationTestHost.InvokeServerToolAsync(tool, TestContext.Current.CancellationToken, "workspace-open", new Dictionary<string, JsonElement>
        {
            ["path"] = JsonSerializer.SerializeToElement(new
            {
                value = "Path",
            }),
        });

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("UnhandledException");
    }

    [Fact]
    public async Task GIVEN_ThrowingLifecycleHandler_WHEN_InvokingThroughMcp_THEN_ShouldReturnStructuredExecutionError()
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
        var tool = new WorkspaceStatusTool(
            CreateStartupOptions(),
            McpIntegrationTestHost.CreateProtocolFactory(),
            service.Object);

        var result = await McpIntegrationTestHost.InvokeServerToolAsync(tool, TestContext.Current.CancellationToken, "workspace-status", new Dictionary<string, JsonElement>());

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("UnhandledException");
    }

    private static IOptions<StartupOptions> CreateStartupOptions()
    {
        return Options.Create(new StartupOptions());
    }
}
