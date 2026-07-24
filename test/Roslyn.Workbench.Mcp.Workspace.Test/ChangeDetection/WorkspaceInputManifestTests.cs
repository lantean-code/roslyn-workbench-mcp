using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceInputManifestTests
{
    [Fact]
    public void GIVEN_ChangeMonitor_WHEN_DisposingManifest_THEN_ShouldDisposeMonitor()
    {
        var changeMonitor = new Mock<IWorkspaceInputChangeMonitor>();
        var target = new WorkspaceInputManifest
        {
            ChangeMonitor = changeMonitor.Object,
        };

        target.Dispose();

        changeMonitor.Verify(item => item.Dispose(), Times.Once);
    }
}
