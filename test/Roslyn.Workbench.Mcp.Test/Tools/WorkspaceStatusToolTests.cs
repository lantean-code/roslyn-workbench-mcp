using System.Text.Json;

using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class WorkspaceStatusToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_OptionalWorkspace_WHEN_GettingStatus_THEN_ShouldRouteAndMapResult(bool includeWorkspace)
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        service
            .Setup(item => item.GetStatusAsync(
                ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
                StatusDetailLevel.Full,
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult<WorkspaceStatusOutcome>.Succeeded(new WorkspaceStatusOutcome
            {
                State = WorkspaceLifecycleState.Ready,
                Workspace = new WorkspaceIdentity
                {
                    WorkspaceId = "WorkspaceId",
                    WorkspaceEpoch = 5,
                    LoadedPath = "/workspace/Sample.csproj",
                },
                ReloadRequired = true,
                Transaction = new TransactionInfo
                {
                    Revision = 9,
                },
                Instances =
                    [
                        new WorkspaceInstanceInfo
                        {
                            InstanceId = "other-instance",
                            LoadedPath = "/workspace/Sample.csproj",
                            WorkspaceRoot = "/workspace",
                            WorkspaceState = WorkspaceLifecycleState.TransactionActive,
                            TransactionRevision = 4,
                            CommitPhase = "Applying",
                        },
                    ],
            }));

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new WorkspaceStatusTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);
        var arguments = ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace);
        arguments["detail"] = JsonSerializer.SerializeToElement(StatusDetailLevel.Full);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-status",
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("state").GetString().Should().Be("Ready");
        data.GetProperty("reloadRequired").GetBoolean().Should().BeTrue();
        data.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(9);
        data.GetProperty("instances")[0].GetProperty("instanceId").GetString().Should().Be("other-instance");
        data.GetProperty("instances")[0].GetProperty("commitPhase").GetString().Should().Be("Applying");
        service.Verify(item => item.GetStatusAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            StatusDetailLevel.Full,
            CancellationToken.None), Times.Once);

        protocolFactory.Verify(item => item.CreateServerOwnedTool<WorkspaceStatusRequest, WorkspaceStatusData>(
            "workspace-status",
            "Workspace Status",
            "Reports the selected workspace lifecycle and cross-instance state. Treat a workspace that is or may be in use elsewhere as query-only, use it only when necessary, and expect results to become stale.",
            true,
            false,
            null,
            ToolOutputSchemaMode.Omit), Times.Once);
    }
}
