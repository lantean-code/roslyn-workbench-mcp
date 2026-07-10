using System.Text.Json;

using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Tools;
using Roslyn.Workbench.Mcp.Workspace.Lifecycle;
using Roslyn.Workbench.Mcp.Workspace.Operations;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class WorkspaceLifecycleToolUnitTests
{
    [Fact]
    public async Task GIVEN_OpenRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMappedWorkspaceOpenResult()
    {
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>();
        var request = new WorkspaceOpenRequest
        {
            Path = "/workspace/Sample.csproj",
            Alias = "Alias",
        };
        var target = new WorkspaceOpenTool(Options.Create(new StartupOptions()), workspaceLifecycleService.Object);

        workspaceLifecycleService
            .Setup(service => service.OpenAsync(request.Path, request.Alias, CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<WorkspaceOpenOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new WorkspaceOpenOutcome
                {
                    Workspace = new WorkspaceIdentity
                    {
                        WorkspaceId = "WorkspaceId",
                        Alias = "Alias",
                        WorkspaceEpoch = 3,
                        LoadedPath = "/workspace/Sample.csproj",
                    },
                    ProjectCount = 2,
                    DocumentCount = 5,
                },
            });

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-open",
            new Dictionary<string, JsonElement>
            {
                ["path"] = JsonSerializer.SerializeToElement(request.Path),
                ["alias"] = JsonSerializer.SerializeToElement(request.Alias),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("workspace").GetProperty("workspaceId").GetString().Should().Be("WorkspaceId");
        result.StructuredContent.Value.GetProperty("projectCount").GetInt32().Should().Be(2);
        result.StructuredContent.Value.GetProperty("documentCount").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task GIVEN_CloseRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMappedWorkspaceCloseResult()
    {
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>();
        var request = new WorkspaceCloseRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = "WorkspaceId",
                Alias = "Alias",
                Path = "/workspace/Sample.csproj",
            },
        };
        var target = new WorkspaceCloseTool(Options.Create(new StartupOptions()), workspaceLifecycleService.Object);

        workspaceLifecycleService
            .Setup(service => service.CloseAsync("WorkspaceId", "Alias", "/workspace/Sample.csproj", CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<WorkspaceCloseOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new WorkspaceCloseOutcome
                {
                    ClosedPath = "/workspace/Sample.csproj",
                },
            });

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-close",
            new Dictionary<string, JsonElement>
            {
                ["workspace"] = JsonSerializer.SerializeToElement(request.Workspace),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("closedPath").GetString().Should().Be("/workspace/Sample.csproj");
    }

    [Fact]
    public async Task GIVEN_ReloadRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMappedWorkspaceReloadResult()
    {
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>();
        var request = new WorkspaceReloadRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = "WorkspaceId",
                Alias = "Alias",
                Path = "/workspace/Sample.csproj",
            },
        };
        var target = new WorkspaceReloadTool(Options.Create(new StartupOptions()), workspaceLifecycleService.Object);

        workspaceLifecycleService
            .Setup(service => service.ReloadAsync("WorkspaceId", "Alias", "/workspace/Sample.csproj", CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<WorkspaceReloadOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new WorkspaceReloadOutcome
                {
                    Workspace = new WorkspaceIdentity
                    {
                        WorkspaceId = "WorkspaceId",
                        Alias = "Alias",
                        WorkspaceEpoch = 4,
                        LoadedPath = "/workspace/Sample.csproj",
                    },
                    ProjectCount = 4,
                    DocumentCount = 10,
                },
            });

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-reload",
            new Dictionary<string, JsonElement>
            {
                ["workspace"] = JsonSerializer.SerializeToElement(request.Workspace),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("workspace").GetProperty("workspaceEpoch").GetInt64().Should().Be(4);
        result.StructuredContent.Value.GetProperty("projectCount").GetInt32().Should().Be(4);
        result.StructuredContent.Value.GetProperty("documentCount").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task GIVEN_StatusRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMappedWorkspaceStatusResult()
    {
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>();
        var request = new WorkspaceStatusRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = "WorkspaceId",
                Alias = "Alias",
                Path = "/workspace/Sample.csproj",
            },
            Detail = StatusDetailLevel.Full,
        };
        var target = new WorkspaceStatusTool(Options.Create(new StartupOptions()), workspaceLifecycleService.Object);

        workspaceLifecycleService
            .Setup(service => service.GetStatusAsync("WorkspaceId", "Alias", "/workspace/Sample.csproj", StatusDetailLevel.Full, CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<WorkspaceStatusOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new WorkspaceStatusOutcome
                {
                    State = WorkspaceLifecycleState.Ready,
                    Workspace = new WorkspaceIdentity
                    {
                        WorkspaceId = "WorkspaceId",
                        Alias = "Alias",
                        WorkspaceEpoch = 5,
                        LoadedPath = "/workspace/Sample.csproj",
                    },
                    ProjectCount = 4,
                    DocumentCount = 10,
                    ReloadRequired = true,
                    Transaction = new TransactionInfo
                    {
                        Revision = 9,
                    },
                },
            });

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-status",
            new Dictionary<string, JsonElement>
            {
                ["workspace"] = JsonSerializer.SerializeToElement(request.Workspace),
                ["detail"] = JsonSerializer.SerializeToElement(request.Detail),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("state").GetString().Should().Be("Ready");
        result.StructuredContent.Value.GetProperty("reloadRequired").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(9);
    }
}
