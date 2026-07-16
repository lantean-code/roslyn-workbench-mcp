using System.IO.Abstractions;
using Roslyn.Workbench.Mcp.Workspace.IO;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginPackagePathPolicyTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IFile> _file;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IPath> _path;
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly PluginPackagePathPolicy _target;

    public PluginPackagePathPolicyTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _file = new Mock<IFile>();
        _directory = new Mock<IDirectory>();
        _path = new Mock<IPath>();
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _fileSystem.SetupGet(static value => value.File).Returns(_file.Object);
        _fileSystem.SetupGet(static value => value.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(static value => value.Path).Returns(_path.Object);
        _path.Setup(static value => value.GetFullPath(It.IsAny<string>())).Returns(static (string value) => value);
        _path.Setup(static value => value.IsPathRooted(It.IsAny<string>())).Returns(false);
        _path.SetupGet(static value => value.DirectorySeparatorChar).Returns('/');
        _path.SetupGet(static value => value.AltDirectorySeparatorChar).Returns('\\');
        _pathComparison.SetupGet(static value => value.Comparison).Returns(StringComparison.Ordinal);
        _pathComparison.SetupGet(static value => value.Comparer).Returns(StringComparer.Ordinal);
        _target = new PluginPackagePathPolicy(_fileSystem.Object, _pathComparison.Object);
    }

    [Fact]
    public void GIVEN_ContainedPathWithLinksRemainingInsidePackage_WHEN_Validating_THEN_ShouldReturnCanonicalPath()
    {
        const string packageDirectory = "/packages/plugin";
        const string candidatePath = "/packages/plugin/lib/tool.dll";
        var directoryTarget = new Mock<IFileSystemInfo>();
        var fileTarget = new Mock<IFileSystemInfo>();
        var packageTarget = new Mock<IFileSystemInfo>();
        directoryTarget.SetupGet(static value => value.FullName).Returns("/packages/plugin/lib");
        fileTarget.SetupGet(static value => value.FullName).Returns("/packages/plugin/tool.dll");
        packageTarget.SetupGet(static value => value.FullName).Returns(packageDirectory);
        _path.Setup(static value => value.GetRelativePath(packageDirectory, candidatePath)).Returns("lib/tool.dll");
        _path.Setup(static value => value.Combine(packageDirectory, "lib")).Returns("/packages/plugin/lib");
        _path.Setup(static value => value.Combine("/packages/plugin/lib", "tool.dll")).Returns(candidatePath);
        _path.Setup(static value => value.GetRelativePath(packageDirectory, "/packages/plugin/lib")).Returns("lib");
        _path.Setup(static value => value.GetRelativePath(packageDirectory, "/packages/plugin/tool.dll")).Returns("tool.dll");
        _directory.Setup(static value => value.Exists(packageDirectory)).Returns(true);
        _directory.Setup(static value => value.ResolveLinkTarget(packageDirectory, true)).Returns(packageTarget.Object);
        _directory.Setup(static value => value.Exists("/packages/plugin/lib")).Returns(true);
        _directory.Setup(static value => value.ResolveLinkTarget("/packages/plugin/lib", true)).Returns(directoryTarget.Object);
        _directory.Setup(static value => value.Exists(candidatePath)).Returns(false);
        _file.Setup(static value => value.Exists(candidatePath)).Returns(true);
        _file.Setup(static value => value.ResolveLinkTarget(candidatePath, true)).Returns(fileTarget.Object);

        var result = _target.TryGetContainedPath(packageDirectory, candidatePath, out var containedPath);

        result.Should().BeTrue();
        containedPath.Should().Be(candidatePath);
        _target.Comparer.Should().BeSameAs(StringComparer.Ordinal);
    }

    [Fact]
    public void GIVEN_ContainedNonExistingPath_WHEN_Validating_THEN_ShouldTreatUnresolvedSegmentAsContained()
    {
        const string packageDirectory = "/packages/plugin";
        const string candidatePath = "/packages/plugin/tool.dll";
        _path.Setup(static value => value.GetRelativePath(packageDirectory, candidatePath)).Returns("tool.dll");
        _path.Setup(static value => value.Combine(packageDirectory, "tool.dll")).Returns(candidatePath);
        _directory.Setup(static value => value.Exists(packageDirectory)).Returns(false);
        _directory.Setup(static value => value.Exists(candidatePath)).Returns(false);
        _file.Setup(static value => value.Exists(candidatePath)).Returns(false);

        var result = _target.TryGetContainedPath(packageDirectory, candidatePath, out var containedPath);

        result.Should().BeTrue();
        containedPath.Should().Be(candidatePath);
    }

    [Fact]
    public void GIVEN_ParentRelativePath_WHEN_Validating_THEN_ShouldRejectPath()
    {
        _path.Setup(static value => value.GetRelativePath("/packages/plugin", "/packages/outside.dll")).Returns("../outside.dll");

        var result = _target.TryGetContainedPath("/packages/plugin", "/packages/outside.dll", out var containedPath);

        result.Should().BeFalse();
        containedPath.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_ExactParentRelativePath_WHEN_Validating_THEN_ShouldRejectPath()
    {
        _path.Setup(static value => value.GetRelativePath("/packages/plugin", "/packages")).Returns("..");

        var result = _target.TryGetContainedPath("/packages/plugin", "/packages", out var containedPath);

        result.Should().BeFalse();
        containedPath.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_AlternateSeparatorParentPath_WHEN_Validating_THEN_ShouldRejectPath()
    {
        _path.Setup(static value => value.GetRelativePath("C:\\packages\\plugin", "C:\\packages\\outside.dll"))
            .Returns("..\\outside.dll");

        var result = _target.TryGetContainedPath(
            "C:\\packages\\plugin",
            "C:\\packages\\outside.dll",
            out var containedPath);

        result.Should().BeFalse();
        containedPath.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_RootedRelativePath_WHEN_Validating_THEN_ShouldRejectPath()
    {
        _path.Setup(static value => value.GetRelativePath("C:\\packages\\plugin", "D:\\plugin.dll"))
            .Returns("D:\\plugin.dll");
        _path.Setup(static value => value.IsPathRooted("D:\\plugin.dll")).Returns(true);

        var result = _target.TryGetContainedPath("C:\\packages\\plugin", "D:\\plugin.dll", out var containedPath);

        result.Should().BeFalse();
        containedPath.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_DirectoryLinkEscapesPackage_WHEN_Validating_THEN_ShouldRejectPath()
    {
        const string packageDirectory = "/packages/plugin";
        const string candidatePath = "/packages/plugin/lib/tool.dll";
        var linkTarget = new Mock<IFileSystemInfo>();
        linkTarget.SetupGet(static value => value.FullName).Returns("/outside/lib");
        _path.Setup(static value => value.GetRelativePath(packageDirectory, candidatePath)).Returns("lib/tool.dll");
        _path.Setup(static value => value.Combine(packageDirectory, "lib")).Returns("/packages/plugin/lib");
        _path.Setup(static value => value.GetRelativePath(packageDirectory, "/outside/lib")).Returns("../../outside/lib");
        _directory.Setup(static value => value.Exists(packageDirectory)).Returns(false);
        _directory.Setup(static value => value.Exists("/packages/plugin/lib")).Returns(true);
        _directory.Setup(static value => value.ResolveLinkTarget("/packages/plugin/lib", true)).Returns(linkTarget.Object);

        var result = _target.TryGetContainedPath(packageDirectory, candidatePath, out var containedPath);

        result.Should().BeFalse();
        containedPath.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_InvalidPath_WHEN_Validating_THEN_ShouldRejectPath()
    {
        _path.Setup(static value => value.GetFullPath("invalid")).Throws(new ArgumentException("Invalid"));

        var result = _target.TryGetContainedPath("package", "invalid", out var containedPath);

        result.Should().BeFalse();
        containedPath.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_UnsupportedPath_WHEN_Validating_THEN_ShouldRejectPath()
    {
        _path.Setup(static value => value.GetFullPath("unsupported")).Throws(new NotSupportedException("Unsupported"));

        var result = _target.TryGetContainedPath("package", "unsupported", out var containedPath);

        result.Should().BeFalse();
        containedPath.Should().BeEmpty();
    }
}
