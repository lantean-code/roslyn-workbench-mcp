using Microsoft.Extensions.Options;

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
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(new WorkspaceListOutcome
            {
                Workspaces =
                    [
                        new WorkspaceIdentity
                        {
                            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                            Alias = "Alias",
                            WorkspaceEpoch = 7,
                            LoadedPath = "/workspace/Sample.csproj",
                        },
                    ],
                TransactionOwnerWorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            }));

        var result = await ServerOwnedToolTestSupport.InvokeAsync(target, "workspace-list", cancellationToken: CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        var data = result.StructuredContent.Value.GetProperty("data");
        data.GetProperty("workspaces").GetArrayLength().Should().Be(1);
        data.GetProperty("workspaces")[0].GetProperty("workspaceId").GetString().Should().Be("11111111-1111-1111-1111-111111111111");
        data.GetProperty("workspaces")[0].GetProperty("alias").GetString().Should().Be("Alias");
        data.GetProperty("transactionOwnerWorkspaceId").GetString().Should().Be("11111111-1111-1111-1111-111111111111");
        workspaceLifecycleService.Verify(service => service.ListAsync(CancellationToken.None), Times.Once);
    }
}
