using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Recovery;

#pragma warning disable CA1416 // Moq expression trees configure Unix-only members without invoking the operating-system API.
public sealed class WorkspaceStateDirectorySecurityTests
{
    private const UnixFileMode _privateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode _privateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IFile> _file;
    private readonly WorkspaceStateDirectorySecurity _target;

    public WorkspaceStateDirectorySecurityTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _directory = new Mock<IDirectory>();
        _file = new Mock<IFile>();
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.File).Returns(_file.Object);
        _target = new WorkspaceStateDirectorySecurity(_fileSystem.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GIVEN_InvalidPath_WHEN_EnsuringDirectory_THEN_ShouldRejectIt(string path)
    {
        var action = () => _target.EnsureDirectory(path);

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GIVEN_InvalidPath_WHEN_ValidatingFile_THEN_ShouldRejectIt(string path)
    {
        var action = () => _target.ValidateFile(path);

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GIVEN_InvalidPath_WHEN_ValidatingDirectory_THEN_ShouldRejectIt(string path)
    {
        var action = () => _target.ValidateDirectory(path);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GIVEN_PrivateDirectory_WHEN_EnsuringDirectory_THEN_ShouldCreateAndValidateIt()
    {
        _file.Setup(item => item.GetAttributes("Directory")).Returns(FileAttributes.Directory);
        _file.Setup(item => item.GetUnixFileMode("Directory")).Returns(_privateDirectoryMode);

        _target.EnsureDirectory("Directory");

        if (OperatingSystem.IsWindows())
        {
            _directory.Verify(item => item.CreateDirectory("Directory"), Times.Once);
            _directory.Verify(
                item => item.CreateDirectory(It.IsAny<string>(), It.IsAny<UnixFileMode>()),
                Times.Never);
        }
        else
        {
            _directory.Verify(
                item => item.CreateDirectory("Directory", _privateDirectoryMode),
                Times.Once);

            _directory.Verify(item => item.CreateDirectory(It.IsAny<string>()), Times.Never);
            _file.Verify(item => item.GetUnixFileMode("Directory"), Times.Once);
        }
    }

    [Fact]
    public void GIVEN_RedirectedDirectory_WHEN_EnsuringDirectory_THEN_ShouldRejectIt()
    {
        _file
            .Setup(item => item.GetAttributes("Directory"))
            .Returns(FileAttributes.Directory | FileAttributes.ReparsePoint);

        var action = () => _target.EnsureDirectory("Directory");

        action.Should().Throw<IOException>()
            .WithMessage("*symbolic link or reparse point*");

        _file.Verify(item => item.GetUnixFileMode(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_PrivateDirectory_WHEN_ValidatingDirectory_THEN_ShouldNotCreateIt()
    {
        _file.Setup(item => item.GetAttributes("Directory")).Returns(FileAttributes.Directory);
        _file.Setup(item => item.GetUnixFileMode("Directory")).Returns(_privateDirectoryMode);

        _target.ValidateDirectory("Directory");

        _directory.Verify(
            item => item.CreateDirectory(It.IsAny<string>()),
            Times.Never);

        _directory.Verify(
            item => item.CreateDirectory(It.IsAny<string>(), It.IsAny<UnixFileMode>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_BroadUnixDirectoryMode_WHEN_EnsuringDirectory_THEN_ShouldRejectIt()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _file.Setup(item => item.GetAttributes("Directory")).Returns(FileAttributes.Directory);
        _file.Setup(item => item.GetUnixFileMode("Directory")).Returns(
            _privateDirectoryMode | UnixFileMode.OtherRead);

        var action = () => _target.EnsureDirectory("Directory");

        action.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*Unix permissions '700'*");
    }

    [Fact]
    public void GIVEN_PrivateFile_WHEN_ValidatingFile_THEN_ShouldAcceptIt()
    {
        _file.Setup(item => item.GetAttributes("File")).Returns(FileAttributes.Normal);
        _file.Setup(item => item.GetUnixFileMode("File")).Returns(_privateFileMode);

        _target.ValidateFile("File");

        if (!OperatingSystem.IsWindows())
        {
            _file.Verify(item => item.GetUnixFileMode("File"), Times.Once);
        }
    }

    [Fact]
    public void GIVEN_BroadUnixFileMode_WHEN_ValidatingFile_THEN_ShouldRejectIt()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _file.Setup(item => item.GetAttributes("File")).Returns(FileAttributes.Normal);
        _file.Setup(item => item.GetUnixFileMode("File")).Returns(
            _privateFileMode | UnixFileMode.GroupRead);

        var action = () => _target.ValidateFile("File");

        action.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*Unix permissions '600'*");
    }
}
