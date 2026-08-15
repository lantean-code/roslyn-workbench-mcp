using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceInputCertificationTests : IDisposable
{
    private readonly Mock<IWorkspaceInputChangeMonitor> _changeMonitor;
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly WorkspaceInputCertification _target;

    public WorkspaceInputCertificationTests()
    {
        _changeMonitor = new Mock<IWorkspaceInputChangeMonitor>();
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: true));

        _target = new WorkspaceInputCertification(
            _changeMonitor.Object,
            _pathComparison.Object);
    }

    [Fact]
    public void GIVEN_NewCertification_WHEN_Constructed_THEN_ShouldStartMonitoringImmediately()
    {
        _changeMonitor.Verify(item => item.Start(), Times.Once);
    }

    [Fact]
    public void GIVEN_ActiveCertification_WHEN_Completed_THEN_ShouldAttachAndConfigureMonitor()
    {
        using var manifest = new WorkspaceInputManifest();

        var result = _target.Complete(manifest);

        result.ChangeMonitor.Should().BeSameAs(_changeMonitor.Object);
        _changeMonitor.Verify(item => item.Track(result), Times.Once);

        _target.Dispose();
        _changeMonitor.Verify(item => item.Dispose(), Times.Never);
        result.Dispose();
        _changeMonitor.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_ActiveCertification_WHEN_DisposedBeforeCompletion_THEN_ShouldDisposeMonitor()
    {
        _target.Dispose();
        _target.Dispose();

        _changeMonitor.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_CompletedCertification_WHEN_CompletedAgain_THEN_ShouldRejectReuse()
    {
        using var manifest = new WorkspaceInputManifest();
        using var result = _target.Complete(manifest);
        using var secondManifest = new WorkspaceInputManifest();

        var action = () => _target.Complete(secondManifest);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Workspace input certification has already completed or been disposed.");
    }

    [Fact]
    public void GIVEN_MonitorConfigurationFailure_WHEN_Completing_THEN_ShouldDisposeMonitor()
    {
        using var manifest = new WorkspaceInputManifest();
        _changeMonitor.Setup(item => item.Track(It.IsAny<WorkspaceInputManifest>()))
            .Throws(new IOException());

        var action = () => _target.Complete(manifest);

        action.Should().Throw<IOException>();
        _changeMonitor.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_IgnoredPaths_WHEN_Completing_THEN_ShouldAttachAComparerAwareCopy()
    {
        using var manifest = new WorkspaceInputManifest();

        using var result = _target.Complete(manifest, ["/Workspace/Document.cs"]);

        var expectedPath = new FileSystemPathKey("/Workspace/Document.cs", isCaseSensitive: true);
        result.IgnoredPaths.Should().Contain(expectedPath);
        result.IgnoredPaths.Should().NotBeSameAs(manifest.IgnoredPaths);
        _changeMonitor.Verify(item => item.Track(result), Times.Once);
    }

    public void Dispose()
    {
        _target.Dispose();
    }
}
