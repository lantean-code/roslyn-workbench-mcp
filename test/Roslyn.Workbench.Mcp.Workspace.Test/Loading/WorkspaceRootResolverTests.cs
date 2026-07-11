using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceRootResolverTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IFile> _file;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IPath> _path;
    private readonly WorkspaceRootResolver _target;

    public WorkspaceRootResolverTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _file = new Mock<IFile>();
        _directory = new Mock<IDirectory>();
        _path = new Mock<IPath>();
        _fileSystem.SetupGet(item => item.File).Returns(_file.Object);
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path.Setup(item => item.IsPathFullyQualified(It.IsAny<string>())).Returns((string value) => Path.IsPathFullyQualified(value));
        _path.Setup(item => item.GetFullPath(It.IsAny<string>())).Returns((string value) => Path.GetFullPath(value));
        _path.Setup(item => item.GetDirectoryName(It.IsAny<string>())).Returns((string value) => Path.GetDirectoryName(value));
        _path.Setup(item => item.Combine(It.IsAny<string>(), It.IsAny<string>())).Returns((string left, string right) => Path.Combine(left, right));
        _path.Setup(item => item.GetRelativePath(It.IsAny<string>(), It.IsAny<string>())).Returns((string root, string value) => Path.GetRelativePath(root, value));
        _path.Setup(item => item.IsPathRooted(It.IsAny<string>())).Returns((string value) => Path.IsPathRooted(value));
        _path.SetupGet(item => item.DirectorySeparatorChar).Returns(Path.DirectorySeparatorChar);
        _target = new WorkspaceRootResolver(_fileSystem.Object);
    }

    [Fact]
    public void GIVEN_ExplicitContainingRoot_WHEN_Resolving_THEN_ShouldUseCanonicalExplicitRoot()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repository"));
        var loadedPath = Path.Combine(root, "src", "Project.csproj");
        _directory.Setup(item => item.Exists(root)).Returns(true);

        var result = _target.Resolve(loadedPath, root);

        result.Should().Be(root);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_GitMarkerAboveProject_WHEN_Resolving_THEN_ShouldUseRepositoryRoot(bool markerIsDirectory)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repository"));
        var marker = Path.Combine(root, ".git");
        var loadedPath = Path.Combine(root, "src", "Project", "Project.csproj");
        if (markerIsDirectory)
        {
            _directory.Setup(item => item.Exists(marker)).Returns(true);
        }
        else
        {
            _file.Setup(item => item.Exists(marker)).Returns(true);
        }

        var result = _target.Resolve(loadedPath, requestedRoot: null);

        result.Should().Be(root);
    }

    [Fact]
    public void GIVEN_NoRepositoryMarker_WHEN_Resolving_THEN_ShouldUseLoadedPathDirectory()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project"));
        var loadedPath = Path.Combine(root, "Project.csproj");

        var result = _target.Resolve(loadedPath, requestedRoot: null);

        result.Should().Be(root);
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("")]
    public void GIVEN_InvalidExplicitRoot_WHEN_Resolving_THEN_ShouldRejectIt(string requestedRoot)
    {
        var loadedPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "Project.csproj"));

        var result = _target.Resolve(loadedPath, requestedRoot);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_PathOutsideRoot_WHEN_CheckingContainment_THEN_ShouldReturnFalse()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "first"));
        var path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "second", "Project.csproj"));

        var result = _target.Contains(root, path);

        result.Should().BeFalse();
    }
}
