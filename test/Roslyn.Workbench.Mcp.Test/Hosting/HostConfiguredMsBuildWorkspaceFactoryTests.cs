using Microsoft.CodeAnalysis.Host.Mef;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class HostConfiguredMsBuildWorkspaceFactoryTests
{
    private readonly Mock<ICodeActionComposition> _composition;
    private readonly HostConfiguredMsBuildWorkspaceFactory _target;

    public HostConfiguredMsBuildWorkspaceFactoryTests()
    {
        _composition = new Mock<ICodeActionComposition>();
        _target = new HostConfiguredMsBuildWorkspaceFactory(_composition.Object);
    }

    [Fact]
    public void GIVEN_NoComposedHostServices_WHEN_CreatingWorkspace_THEN_ShouldCreateDefaultWorkspace()
    {
        using var result = _target.Create();

        result.Should().NotBeNull();
        result.SkipUnrecognizedProjects.Should().BeTrue();
        _composition.VerifyGet(item => item.WorkspaceHostServices, Times.Once);
    }

    [Fact]
    public void GIVEN_ComposedHostServices_WHEN_CreatingWorkspace_THEN_ShouldCreateConfiguredWorkspace()
    {
        var hostServices = MefHostServices.Create(MefHostServices.DefaultAssemblies);
        _composition.SetupGet(item => item.WorkspaceHostServices).Returns(hostServices);

        using var result = _target.Create();

        result.Should().NotBeNull();
        result.SkipUnrecognizedProjects.Should().BeTrue();
        _composition.VerifyGet(item => item.WorkspaceHostServices, Times.Once);
    }
}
