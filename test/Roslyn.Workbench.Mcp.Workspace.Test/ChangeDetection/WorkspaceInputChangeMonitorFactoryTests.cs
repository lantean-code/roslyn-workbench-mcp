using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceInputChangeMonitorFactoryTests
{
    [Fact]
    public void GIVEN_WorkspaceRoot_WHEN_CreatingMonitor_THEN_ShouldCreateDisabledFileSystemWatcher()
    {
        var fileSystem = new Mock<IFileSystem>();
        var watcherFactory = new Mock<IFileSystemWatcherFactory>();
        var watcher = new Mock<IFileSystemWatcher>();
        var pathComparison = new Mock<IWorkspacePathComparison>();
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "Workspace");
        fileSystem.SetupGet(item => item.FileSystemWatcher).Returns(watcherFactory.Object);
        watcherFactory.Setup(item => item.New(workspaceRoot)).Returns(watcher.Object);
        var target = new WorkspaceInputChangeMonitorFactory(
            fileSystem.Object,
            pathComparison.Object);

        using var result = target.Create(workspaceRoot);

        result.Should().BeOfType<WorkspaceInputChangeMonitor>();
        watcher.VerifySet(item => item.EnableRaisingEvents = true, Times.Never);
    }
}
