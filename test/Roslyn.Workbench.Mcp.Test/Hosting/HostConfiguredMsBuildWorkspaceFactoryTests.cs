using Microsoft.CodeAnalysis.Host.Mef;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class HostConfiguredMsBuildWorkspaceFactoryTests
{
    private readonly Mock<ICodeActionProviderCatalog> _providerCatalog;
    private readonly HostConfiguredMsBuildWorkspaceFactory _target;

    public HostConfiguredMsBuildWorkspaceFactoryTests()
    {
        _providerCatalog = new Mock<ICodeActionProviderCatalog>();
        _target = new HostConfiguredMsBuildWorkspaceFactory(_providerCatalog.Object);
    }

    [Fact]
    public void GIVEN_NoComposedHostServices_WHEN_CreatingWorkspace_THEN_ShouldCreateDefaultWorkspace()
    {
        using var result = _target.Create();

        result.Should().NotBeNull();
        result.SkipUnrecognizedProjects.Should().BeTrue();
        _providerCatalog.VerifyGet(item => item.WorkspaceHostServices, Times.Once);
    }

    [Fact]
    public void GIVEN_ComposedHostServices_WHEN_CreatingWorkspace_THEN_ShouldCreateConfiguredWorkspace()
    {
        var hostServices = MefHostServices.Create(MefHostServices.DefaultAssemblies);
        _providerCatalog.SetupGet(item => item.WorkspaceHostServices).Returns(hostServices);

        using var result = _target.Create();

        result.Should().NotBeNull();
        result.SkipUnrecognizedProjects.Should().BeTrue();
        _providerCatalog.VerifyGet(item => item.WorkspaceHostServices, Times.Once);
    }
}
