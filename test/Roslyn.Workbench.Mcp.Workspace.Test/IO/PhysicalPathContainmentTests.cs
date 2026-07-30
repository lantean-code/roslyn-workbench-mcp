namespace Roslyn.Workbench.Mcp.Workspace.Test.IO;

public sealed class PhysicalPathContainmentTests
{
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IFile> _file = new();
    private readonly Mock<IDirectory> _directory = new();
    private readonly Mock<IPath> _path = new();
    private readonly Mock<IWorkspacePathComparison> _pathComparison = new();
    private readonly PhysicalPathContainment _target;

    public PhysicalPathContainmentTests()
    {
        _fileSystem.SetupGet(item => item.File).Returns(_file.Object);
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path.Setup(item => item.GetFullPath(It.IsAny<string>())).Returns((string path) => path);
        _path
            .Setup(item => item.GetRelativePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string root, string path) => Path.GetRelativePath(root, path));

        _path.Setup(item => item.IsPathRooted(It.IsAny<string>())).Returns(false);
        _path.SetupGet(item => item.DirectorySeparatorChar).Returns('/');
        _path.SetupGet(item => item.AltDirectorySeparatorChar).Returns('\\');
        _pathComparison
            .Setup(item => item.GetComparison(It.IsAny<string>()))
            .Returns(StringComparison.Ordinal);

        _target = new PhysicalPathContainment(_fileSystem.Object, _pathComparison.Object);
    }

    [Fact]
    public void GIVEN_ContainedPathWithLinksRemainingInsideRoot_WHEN_Validating_THEN_ShouldReturnCanonicalPath()
    {
        const string rootDirectory = "/workspace";
        const string candidatePath = "/workspace/src/file.cs";
        var directoryTarget = CreateFileSystemInfo("/workspace/src");
        var fileTarget = CreateFileSystemInfo("/workspace/other.cs");
        _path.Setup(item => item.GetRelativePath(rootDirectory, candidatePath)).Returns("src/file.cs");
        _path.Setup(item => item.Combine(rootDirectory, "src")).Returns("/workspace/src");
        _path.Setup(item => item.Combine("/workspace/src", "file.cs")).Returns(candidatePath);
        _path.Setup(item => item.GetRelativePath(rootDirectory, "/workspace/src")).Returns("src");
        _path.Setup(item => item.GetRelativePath(rootDirectory, "/workspace/other.cs")).Returns("other.cs");
        _directory.Setup(item => item.Exists(rootDirectory)).Returns(false);
        _directory.Setup(item => item.Exists("/workspace/src")).Returns(true);
        _directory
            .Setup(item => item.ResolveLinkTarget("/workspace/src", true))
            .Returns(directoryTarget.Object);

        _file.Setup(item => item.Exists(candidatePath)).Returns(true);
        _file
            .Setup(item => item.ResolveLinkTarget(candidatePath, true))
            .Returns(fileTarget.Object);

        var result = _target.TryGetContainedPath(rootDirectory, candidatePath, out var containedPath);

        result.Should().BeTrue();
        containedPath.Should().Be(candidatePath);
    }

    [Fact]
    public void GIVEN_DirectoryLinkEscapesRoot_WHEN_Validating_THEN_ShouldRejectPath()
    {
        const string rootDirectory = "/workspace";
        const string candidatePath = "/workspace/src/file.cs";
        var linkTarget = CreateFileSystemInfo("/outside/src");
        _path.Setup(item => item.GetRelativePath(rootDirectory, candidatePath)).Returns("src/file.cs");
        _path.Setup(item => item.Combine(rootDirectory, "src")).Returns("/workspace/src");
        _path.Setup(item => item.GetRelativePath(rootDirectory, "/outside/src")).Returns("../outside/src");
        _directory.Setup(item => item.Exists("/workspace/src")).Returns(true);
        _directory
            .Setup(item => item.ResolveLinkTarget("/workspace/src", true))
            .Returns(linkTarget.Object);

        var result = _target.TryGetContainedPath(rootDirectory, candidatePath, out var containedPath);

        result.Should().BeFalse();
        containedPath.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_RootLink_WHEN_ValidatingContainedPath_THEN_ShouldUseResolvedRoot()
    {
        const string rootDirectory = "/workspace-link";
        const string candidatePath = "/workspace-link/src/file.cs";
        var rootTarget = CreateFileSystemInfo("/physical/workspace");
        _path.Setup(item => item.GetRelativePath(rootDirectory, candidatePath)).Returns("src/file.cs");
        _path.Setup(item => item.Combine("/physical/workspace", "src")).Returns("/physical/workspace/src");
        _path.Setup(item => item.Combine("/physical/workspace/src", "file.cs")).Returns("/physical/workspace/src/file.cs");
        _path.Setup(item => item.GetRelativePath("/physical/workspace", "/physical/workspace/src")).Returns("src");
        _path.Setup(item => item.GetRelativePath("/physical/workspace", "/physical/workspace/src/file.cs")).Returns("src/file.cs");
        _directory.Setup(item => item.Exists(rootDirectory)).Returns(true);
        _directory
            .Setup(item => item.ResolveLinkTarget(rootDirectory, true))
            .Returns(rootTarget.Object);

        var result = _target.TryGetContainedPath(rootDirectory, candidatePath, out var containedPath);

        result.Should().BeTrue();
        containedPath.Should().Be(candidatePath);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void GIVEN_RootPath_WHEN_Validating_THEN_StrictnessShouldDetermineResult(
        bool strict,
        bool expected)
    {
        _path.Setup(item => item.GetRelativePath("/workspace", "/workspace")).Returns(".");

        bool result;
        if (strict)
        {
            result = _target.TryGetStrictlyContainedPath("/workspace", "/workspace", out _);
        }
        else
        {
            result = _target.TryGetContainedPath("/workspace", "/workspace", out _);
        }

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("../outside.cs", false)]
    [InlineData("..", false)]
    [InlineData("..\\outside.cs", false)]
    [InlineData("D:\\outside.cs", true)]
    public void GIVEN_PathOutsideLexicalRoot_WHEN_Validating_THEN_ShouldRejectPath(
        string relativePath,
        bool rooted)
    {
        _path.Setup(item => item.GetRelativePath("/workspace", "/outside.cs")).Returns(relativePath);
        _path.Setup(item => item.IsPathRooted(relativePath)).Returns(rooted);

        var result = _target.TryGetContainedPath("/workspace", "/outside.cs", out var containedPath);

        result.Should().BeFalse();
        containedPath.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GIVEN_PathResolutionFailure_WHEN_Validating_THEN_ShouldRejectPath(int exceptionType)
    {
        _path
            .Setup(item => item.GetFullPath("invalid"))
            .Throws(CreatePathResolutionException(exceptionType));

        var result = _target.TryGetContainedPath("/workspace", "invalid", out var containedPath);

        result.Should().BeFalse();
        containedPath.Should().BeEmpty();
    }

    private static Mock<IFileSystemInfo> CreateFileSystemInfo(string fullName)
    {
        var target = new Mock<IFileSystemInfo>();
        target.SetupGet(item => item.FullName).Returns(fullName);
        return target;
    }

    private static Exception CreatePathResolutionException(int exceptionType)
    {
        return exceptionType switch
        {
            0 => new ArgumentException("invalid"),
            1 => new IOException("unavailable"),
            2 => new NotSupportedException("unsupported"),
            _ => new UnauthorizedAccessException("denied"),
        };
    }
}
