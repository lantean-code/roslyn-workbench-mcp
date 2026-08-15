namespace Roslyn.Workbench.Mcp.Workspace.Test.IO;

public sealed class WorkspacePathComparisonTests
{
    [Fact]
    public void GIVEN_CurrentOperatingSystem_WHEN_CreatingPathKey_THEN_ShouldCapturePlatformDefaultComparison()
    {
        var target = new WorkspacePathComparison();
        var expectedComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var result = target.CreateKey("Path");

        result.Path.Should().Be("Path");
        result.Comparison.Should().Be(expectedComparison);
    }

    [Fact]
    public void GIVEN_DefaultDrvFsMount_WHEN_GettingPathPolicy_THEN_ShouldUseCaseInsensitiveComparison()
    {
        var fileSystem = CreateFileSystem([
            "23 134 0:72 / /mnt/c rw,noatime - 9p C: rw,aname=drvfs;path=C:",
        ]);

        var target = new WorkspacePathComparison(fileSystem.Object);

        var mountComparison = target.GetComparison("/mnt/c");
        var comparison = target.GetComparison("/mnt/c/Users/Developer/Repository");

        mountComparison.Should().Be(StringComparison.OrdinalIgnoreCase);
        comparison.Should().Be(StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GIVEN_WindowsMount_WHEN_InspectingPath_THEN_ShouldIdentifyItOnLinux(bool hasCustomMountPoint)
    {
        var mountInfo = hasCustomMountPoint
            ? "23 134 0:72 / /windows rw,noatime,case=dir - drvfs C: rw"
            : "23 134 0:72 / /mnt/c rw,noatime - 9p C: rw,aname=drvfs;path=C:";

        var fileSystem = CreateFileSystem([mountInfo]);
        var target = new WorkspacePathComparison(fileSystem.Object);
        var expected = OperatingSystem.IsLinux();

        var result = target.IsWindowsFileSystemPath(hasCustomMountPoint
            ? "/windows/Repository"
            : "/mnt/c/Repository");

        result.Should().Be(expected);
    }

    [Fact]
    public void GIVEN_NativeLinuxMountWithinWindowsMount_WHEN_InspectingPath_THEN_ShouldUseMostSpecificMount()
    {
        var fileSystem = CreateFileSystem([
            "23 134 0:72 / /mnt/c rw,noatime - 9p C: rw,aname=drvfs;path=C:",
            "24 23 8:1 / /mnt/c/native rw,relatime - ext4 /dev/sda rw",
        ]);

        var target = new WorkspacePathComparison(fileSystem.Object);

        var result = target.IsWindowsFileSystemPath("/mnt/c/native/Repository");

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("82 1 8:1 / / rw,relatime - ext4 /dev/sda rw")]
    [InlineData("23 134 0:72 / /mnt/c rw,noatime,case=dir - 9p C: rw,aname=drvfs;path=C:")]
    [InlineData("23 134 0:72 / /mnt/c rw,noatime - 9p")]
    [InlineData("malformed")]
    public void GIVEN_CaseSensitiveOrInvalidMount_WHEN_GettingPathPolicy_THEN_ShouldUsePlatformComparison(string mountInfo)
    {
        var fileSystem = CreateFileSystem([mountInfo]);
        var target = new WorkspacePathComparison(fileSystem.Object);
        var expectedComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var comparison = target.GetComparison("/mnt/c/Users/Developer/Repository");

        comparison.Should().Be(expectedComparison);
    }

    [Fact]
    public void GIVEN_MoreSpecificNativeMountWithinDrvFs_WHEN_GettingPathPolicy_THEN_ShouldUseNativeMountPolicy()
    {
        var fileSystem = CreateFileSystem([
            "23 134 0:72 / /mnt/c rw,noatime - 9p C: rw,aname=drvfs;path=C:",
            "24 23 8:1 / /mnt/c/case-sensitive rw,relatime - ext4 /dev/sda rw",
        ]);

        var target = new WorkspacePathComparison(fileSystem.Object);
        var expectedComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var comparison = target.GetComparison("/mnt/c/case-sensitive/Repository");

        comparison.Should().Be(expectedComparison);
    }

    [Fact]
    public void GIVEN_ShorterMountListedAfterMatchingMount_WHEN_GettingPathPolicy_THEN_ShouldRetainMoreSpecificPolicy()
    {
        var fileSystem = CreateFileSystem([
            "23 134 0:72 / /mnt/c rw,noatime - 9p C: rw,aname=drvfs;path=C:",
            "24 23 8:1 / /mnt rw,relatime - ext4 /dev/sda rw",
        ]);

        var target = new WorkspacePathComparison(fileSystem.Object);

        var comparison = target.GetComparison("/mnt/c/Repository");

        comparison.Should().Be(StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GIVEN_BlankPath_WHEN_GettingPathPolicy_THEN_ShouldUsePlatformComparison()
    {
        var fileSystem = CreateFileSystem([
            "23 134 0:72 / /mnt/c rw,noatime - 9p C: rw,aname=drvfs;path=C:",
        ]);

        var target = new WorkspacePathComparison(fileSystem.Object);
        var expectedComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var comparison = target.GetComparison(string.Empty);

        comparison.Should().Be(expectedComparison);
    }

    [Fact]
    public void GIVEN_MountPointContainsEscapedSpace_WHEN_GettingPathPolicy_THEN_ShouldMatchDecodedPath()
    {
        var fileSystem = CreateFileSystem([
            "23 134 0:72 / /windows\\040drive rw,noatime - drvfs C: rw",
        ]);

        var target = new WorkspacePathComparison(fileSystem.Object);

        var comparison = target.GetComparison("/windows drive/Repository");

        comparison.Should().Be(StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("IOException")]
    [InlineData("UnauthorizedAccessException")]
    public void GIVEN_MountInformationUnavailable_WHEN_GettingPathPolicy_THEN_ShouldUsePlatformComparison(string scenario)
    {
        var fileSystem = CreateFileSystem([]);
        if (scenario == "Missing")
        {
            fileSystem.Setup(item => item.File.Exists("/proc/self/mountinfo")).Returns(false);
        }
        else if (scenario == "IOException")
        {
            fileSystem.Setup(item => item.File.ReadAllLines("/proc/self/mountinfo")).Throws<IOException>();
        }
        else
        {
            fileSystem.Setup(item => item.File.ReadAllLines("/proc/self/mountinfo")).Throws<UnauthorizedAccessException>();
        }

        var target = new WorkspacePathComparison(fileSystem.Object);
        var expectedComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var comparison = target.GetComparison("/mnt/c/Repository");

        comparison.Should().Be(expectedComparison);
    }

    private static Mock<IFileSystem> CreateFileSystem(string[] mountInfo)
    {
        var fileSystem = new Mock<IFileSystem>();
        var file = new Mock<IFile>();
        var path = new Mock<IPath>();
        fileSystem.SetupGet(item => item.File).Returns(file.Object);
        fileSystem.SetupGet(item => item.Path).Returns(path.Object);
        file.Setup(item => item.Exists("/proc/self/mountinfo")).Returns(true);
        file.Setup(item => item.ReadAllLines("/proc/self/mountinfo")).Returns(mountInfo);
        path.Setup(item => item.GetFullPath(It.IsAny<string>())).Returns((string value) => Path.GetFullPath(value));
        return fileSystem;
    }
}
