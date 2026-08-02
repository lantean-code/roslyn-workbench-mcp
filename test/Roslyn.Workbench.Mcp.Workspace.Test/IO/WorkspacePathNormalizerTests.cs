namespace Roslyn.Workbench.Mcp.Workspace.Test.IO;

public sealed class WorkspacePathNormalizerTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IPath> _path;
    private readonly WorkspacePathNormalizer _target;

    public WorkspacePathNormalizerTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _path = new Mock<IPath>();
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _target = new WorkspacePathNormalizer(_fileSystem.Object);
    }

    [Fact]
    public void GIVEN_ValidPath_WHEN_GettingFullPath_THEN_ShouldReturnNormalizedPath()
    {
        _path.Setup(item => item.GetFullPath("Path")).Returns("FullPath");

        var succeeded = _target.TryGetFullPath("Path", out var fullPath);

        succeeded.Should().BeTrue();
        fullPath.Should().Be("FullPath");
    }

    [Fact]
    public void GIVEN_ValidPathAndBase_WHEN_GettingFullPath_THEN_ShouldUseExplicitBase()
    {
        _path.Setup(item => item.GetFullPath("Path", "BasePath")).Returns("FullPath");

        var succeeded = _target.TryGetFullPath("Path", "BasePath", out var fullPath);

        succeeded.Should().BeTrue();
        fullPath.Should().Be("FullPath");
    }

    [Theory]
    [InlineData("Argument")]
    [InlineData("IO")]
    [InlineData("NotSupported")]
    [InlineData("Unauthorized")]
    public void GIVEN_PathNormalizationFailure_WHEN_GettingFullPath_THEN_ShouldReturnFalse(string failureKind)
    {
        var exception = CreateException(failureKind);
        _path.Setup(item => item.GetFullPath("Path")).Throws(exception);

        var succeeded = _target.TryGetFullPath("Path", out var fullPath);

        succeeded.Should().BeFalse();
        fullPath.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_UnexpectedFailure_WHEN_GettingFullPath_THEN_ShouldPropagate()
    {
        _path.Setup(item => item.GetFullPath("Path")).Throws<InvalidOperationException>();

        var action = () => _target.TryGetFullPath("Path", out _);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GIVEN_NoWorkspaceRoot_WHEN_NormalizingRelativePath_THEN_ShouldReturnFullSlashPath()
    {
        _path.Setup(item => item.GetFullPath("Path")).Returns(@"C:\Workspace\Path");

        var succeeded = _target.TryGetWorkspaceRelativePath(string.Empty, "Path", out var relativePath);

        succeeded.Should().BeTrue();
        relativePath.Should().Be("C:/Workspace/Path");
    }

    [Fact]
    public void GIVEN_NoWorkspaceRootAndInvalidPath_WHEN_NormalizingRelativePath_THEN_ShouldReturnFalse()
    {
        _path.Setup(item => item.GetFullPath("Path")).Throws<ArgumentException>();

        var succeeded = _target.TryGetWorkspaceRelativePath(string.Empty, "Path", out var relativePath);

        succeeded.Should().BeFalse();
        relativePath.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_InvalidWorkspaceRoot_WHEN_NormalizingRelativePath_THEN_ShouldReturnFalse()
    {
        _path.Setup(item => item.GetFullPath("Root")).Throws<ArgumentException>();

        var succeeded = _target.TryGetWorkspaceRelativePath("Root", "Path", out var relativePath);

        succeeded.Should().BeFalse();
        relativePath.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_InvalidCandidatePath_WHEN_NormalizingRelativePath_THEN_ShouldReturnFalse()
    {
        _path.Setup(item => item.GetFullPath("Root")).Returns("FullRoot");
        _path.Setup(item => item.GetFullPath("Path", "FullRoot")).Throws<ArgumentException>();

        var succeeded = _target.TryGetWorkspaceRelativePath("Root", "Path", out var relativePath);

        succeeded.Should().BeFalse();
        relativePath.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_ValidWorkspacePath_WHEN_NormalizingRelativePath_THEN_ShouldReturnRelativeSlashPath()
    {
        _path.Setup(item => item.GetFullPath("Root")).Returns("FullRoot");
        _path.Setup(item => item.GetFullPath("Path", "FullRoot")).Returns("FullPath");
        _path.Setup(item => item.GetRelativePath("FullRoot", "FullPath")).Returns(@"Folder\Path");

        var succeeded = _target.TryGetWorkspaceRelativePath("Root", "Path", out var relativePath);

        succeeded.Should().BeTrue();
        relativePath.Should().Be("Folder/Path");
    }

    [Fact]
    public void GIVEN_RelativePathFailure_WHEN_NormalizingRelativePath_THEN_ShouldReturnFalse()
    {
        _path.Setup(item => item.GetFullPath("Root")).Returns("FullRoot");
        _path.Setup(item => item.GetFullPath("Path", "FullRoot")).Returns("FullPath");
        _path.Setup(item => item.GetRelativePath("FullRoot", "FullPath")).Throws<IOException>();

        var succeeded = _target.TryGetWorkspaceRelativePath("Root", "Path", out var relativePath);

        succeeded.Should().BeFalse();
        relativePath.Should().BeEmpty();
    }

    private static Exception CreateException(string failureKind)
    {
        return failureKind switch
        {
            "Argument" => new ArgumentException("Argument"),
            "IO" => new IOException("IO"),
            "NotSupported" => new NotSupportedException("NotSupported"),
            _ => new UnauthorizedAccessException("Unauthorized"),
        };
    }
}
