using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Configuration;
using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Recovery;

public sealed class WorkspaceStateDirectoryTests
{
    private readonly Mock<IPath> _path;
    private readonly Mock<IWorkspaceStateDirectorySecurity> _security;
    private readonly WorkspaceStateDirectory _target;

    public WorkspaceStateDirectoryTests()
    {
        var fileSystem = new Mock<IFileSystem>();
        _path = new Mock<IPath>();
        _security = new Mock<IWorkspaceStateDirectorySecurity>();
        fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path.Setup(item => item.GetFullPath("StateDirectory")).Returns("/State");
        _path.Setup(item => item.Combine("/State", "recovery")).Returns("/State/recovery");
        _target = new WorkspaceStateDirectory(
            Options.Create(new WorkspaceOptions { StateDirectory = "StateDirectory" }),
            fileSystem.Object,
            _security.Object);
    }

    [Fact]
    public void GIVEN_ConfiguredStateDirectory_WHEN_Constructing_THEN_ShouldExposeResolvedPaths()
    {
        _target.RootDirectory.Should().Be("/State");
        _target.RecoveryDirectory.Should().Be("/State/recovery");
    }

    [Fact]
    public void GIVEN_StateDirectory_WHEN_Initializing_THEN_ShouldPrepareRootAndRecoveryDirectories()
    {
        _target.Initialize();

        _security.Verify(item => item.EnsureDirectory("/State"), Times.Once);
        _security.Verify(item => item.EnsureDirectory("/State/recovery"), Times.Once);
    }
}
