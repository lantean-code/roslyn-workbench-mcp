using System.Text.Json;

using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Tools;

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
            .ReturnsAsync(new WorkspaceOperationResult<WorkspaceStatusOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new WorkspaceStatusOutcome
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
                },
            });
        var target = new WorkspaceStatusTool(Options.Create(new StartupOptions()), service.Object);
        var arguments = ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace);
        arguments["detail"] = JsonSerializer.SerializeToElement(StatusDetailLevel.Full);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-status",
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("state").GetString().Should().Be("Ready");
        result.StructuredContent.Value.GetProperty("reloadRequired").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(9);
        result.StructuredContent.Value.GetProperty("instances")[0].GetProperty("instanceId").GetString().Should().Be("other-instance");
        result.StructuredContent.Value.GetProperty("instances")[0].GetProperty("commitPhase").GetString().Should().Be("Applying");
        service.Verify(item => item.GetStatusAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            StatusDetailLevel.Full,
            CancellationToken.None), Times.Once);
    }
}
