using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceExternalInputChangeMonitorFactoryTests
{
    [Fact]
    public void GIVEN_Memberships_WHEN_CreatingMonitor_THEN_ShouldReturnExternalMonitor()
    {
        var fileSystem = new Mock<IFileSystem>();
        var pathComparison = new Mock<IWorkspacePathComparison>();
        var target = new WorkspaceExternalInputChangeMonitorFactory(
            fileSystem.Object,
            pathComparison.Object);

        using var result = target.Create([]);

        result.Should().BeOfType<WorkspaceExternalInputChangeMonitor>();
    }
}
