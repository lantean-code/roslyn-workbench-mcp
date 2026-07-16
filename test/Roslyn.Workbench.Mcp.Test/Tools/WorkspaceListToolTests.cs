using System.Text.Json;
using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class WorkspaceListToolTests
{
    [Fact]
    public async Task GIVEN_LoadedWorkspaces_WHEN_CallingExecuteAsync_THEN_ShouldReturnStructuredWorkspaceInventory()
    {
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>();
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new WorkspaceListTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            workspaceLifecycleService.Object);

        workspaceLifecycleService
            .Setup(service => service.ListAsync(CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<WorkspaceListOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new WorkspaceListOutcome
                {
                    Workspaces =
                    [
                        new WorkspaceIdentity
                        {
                            WorkspaceId = "WorkspaceId",
                            Alias = "Alias",
                            WorkspaceEpoch = 7,
                            LoadedPath = "/workspace/Sample.csproj",
                        },
                    ],
                    TransactionOwnerWorkspaceId = "WorkspaceId",
                },
            });

        var result = await ServerOwnedToolTestSupport.InvokeAsync(target, "workspace-list", cancellationToken: CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("workspaces").GetArrayLength().Should().Be(1);
        result.StructuredContent.Value.GetProperty("workspaces")[0].GetProperty("workspaceId").GetString().Should().Be("WorkspaceId");
        result.StructuredContent.Value.GetProperty("workspaces")[0].GetProperty("alias").GetString().Should().Be("Alias");
        result.StructuredContent.Value.GetProperty("transactionOwnerWorkspaceId").GetString().Should().Be("WorkspaceId");
        workspaceLifecycleService.Verify(service => service.ListAsync(CancellationToken.None), Times.Once);
    }

}
