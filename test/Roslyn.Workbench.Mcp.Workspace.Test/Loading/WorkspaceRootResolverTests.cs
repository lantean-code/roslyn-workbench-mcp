using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceRootResolverTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IFile> _file;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IPath> _path;
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly Mock<IPhysicalPathContainment> _pathContainment;
    private readonly WorkspaceRootResolver _target;

    public WorkspaceRootResolverTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _file = new Mock<IFile>();
        _directory = new Mock<IDirectory>();
        _path = new Mock<IPath>();
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _pathContainment = new Mock<IPhysicalPathContainment>();
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
        _path.SetupGet(item => item.AltDirectorySeparatorChar).Returns(Path.AltDirectorySeparatorChar);
        _pathComparison.SetupGet(item => item.Comparison).Returns(StringComparison.Ordinal);
        _pathComparison.Setup(item => item.GetComparison(It.IsAny<string>())).Returns(StringComparison.Ordinal);
        _pathContainment
            .Setup(item => item.TryGetContainedPath(It.IsAny<string>(), It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns((string root, string path, out string containedPath) =>
            {
                containedPath = path;
                return Path.GetFullPath(path).StartsWith(Path.GetFullPath(root), StringComparison.Ordinal);
            });

        _target = new WorkspaceRootResolver(_fileSystem.Object, _pathComparison.Object, _pathContainment.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative")]
    public void GIVEN_InvalidLoadedPath_WHEN_Resolving_THEN_ShouldRejectIt(string loadedPath)
    {
        var result = _target.Resolve(loadedPath, requestedRoot: null);

        result.Should().BeNull();
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
    public void GIVEN_ExplicitRootDoesNotExist_WHEN_Resolving_THEN_ShouldRejectIt()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repository"));
        var loadedPath = Path.Combine(root, "Project.csproj");

        var result = _target.Resolve(loadedPath, root);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_ExplicitRootDoesNotContainLoadedPath_WHEN_Resolving_THEN_ShouldRejectIt()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repository"));
        var loadedPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "other", "Project.csproj"));
        _directory.Setup(item => item.Exists(root)).Returns(true);

        var result = _target.Resolve(loadedPath, root);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_PathParentIsUnchanged_WHEN_Resolving_THEN_ShouldUseLoadedPathDirectory()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repository"));
        var loadedPath = Path.Combine(root, "Project.csproj");
        _path.Setup(item => item.GetDirectoryName(loadedPath)).Returns(root);
        _path.Setup(item => item.GetDirectoryName(root)).Returns(root);

        var result = _target.Resolve(loadedPath, requestedRoot: null);

        result.Should().Be(root);
    }

    [Fact]
    public void GIVEN_PathOutsideRoot_WHEN_CheckingContainment_THEN_ShouldReturnFalse()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "first"));
        var path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "second", "Project.csproj"));

        var result = _target.Contains(root, path);

        result.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_PathEqualsRoot_WHEN_CheckingContainment_THEN_ShouldReturnTrue()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repository"));

        var result = _target.Contains(root, root);

        result.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_FileSystemRoot_WHEN_CheckingDescendantContainment_THEN_ShouldReturnTrue()
    {
        var root = Path.GetFullPath(Path.DirectorySeparatorChar.ToString());
        var path = Path.Combine(root, "workspace", "Project.csproj");

        var result = _target.Contains(root, path);

        result.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_CaseInsensitiveMountedRoot_WHEN_PathCasingDiffers_THEN_ShouldReturnTrue()
    {
        _pathContainment
            .Setup(item => item.TryGetContainedPath(
                "/mnt/c/Users/Developer/Repository",
                "/mnt/c/users/developer/repository/src/Project.csproj",
                out It.Ref<string>.IsAny))
            .Returns((string _, string path, out string containedPath) =>
            {
                containedPath = path;
                return true;
            });

        var result = _target.Contains(
            "/mnt/c/Users/Developer/Repository",
            "/mnt/c/users/developer/repository/src/Project.csproj");

        result.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_CaseSensitiveNativeRoot_WHEN_PathCasingDiffers_THEN_ShouldReturnFalse()
    {
        var result = _target.Contains(
            "/home/Developer/Repository",
            "/home/developer/repository/src/Project.csproj");

        result.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_PathIsParentOfRoot_WHEN_CheckingContainment_THEN_ShouldReturnFalse()
    {
        _path.Setup(item => item.GetRelativePath(It.IsAny<string>(), It.IsAny<string>())).Returns("..");

        var result = _target.Contains("/workspace/project", "/workspace");

        result.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_RelativePathIsRooted_WHEN_CheckingContainment_THEN_ShouldReturnFalse()
    {
        var rootedPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "Project.csproj"));
        _path.Setup(item => item.GetRelativePath(It.IsAny<string>(), It.IsAny<string>())).Returns(rootedPath);

        var result = _target.Contains("/workspace", rootedPath);

        result.Should().BeFalse();
    }
}
