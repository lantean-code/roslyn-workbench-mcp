using System.Text.Json;
using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class WorkspaceListToolTests
{
    [Fact]
    public async Task GIVEN_LoadedWorkspaces_WHEN_CallingExecuteAsync_THEN_ShouldReturnStructuredWorkspaceInventory()
    {
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>();
        var target = new WorkspaceListTool(Options.Create(new StartupOptions()), workspaceLifecycleService.Object);

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

    [Fact]
    public void GIVEN_DefaultOutputSchemaMode_WHEN_CallingExecuteAsync_THEN_ShouldOmitOutputSchema()
    {
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>();
        var target = new WorkspaceListTool(Options.Create(new StartupOptions()), workspaceLifecycleService.Object);

        target.ProtocolTool.OutputSchema.Should().BeNull();
        target.ProtocolTool.Description.Should().Be("Lists the currently loaded workspaces.");
    }

    [Fact]
    public void GIVEN_FullOutputSchemaMode_WHEN_CallingExecuteAsync_THEN_ShouldPublishOutputSchema()
    {
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>();
        var target = new WorkspaceListTool(
            Options.Create(new StartupOptions
            {
                ToolOutputSchemaMode = ToolOutputSchemaMode.Full,
            }),
            workspaceLifecycleService.Object);

        target.ProtocolTool.OutputSchema.Should().NotBeNull();
        target.ProtocolTool.OutputSchema!.Value.GetProperty("oneOf").ValueKind.Should().Be(JsonValueKind.Array);
    }
}
