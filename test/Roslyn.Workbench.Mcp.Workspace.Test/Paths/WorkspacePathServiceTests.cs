namespace Roslyn.Workbench.Mcp.Workspace.Test.Paths;

public sealed class WorkspacePathServiceTests
{
    [Fact]
    public void GIVEN_WorkspaceRelativeAndAbsolutePaths_WHEN_Normalizing_THEN_ShouldReturnWorkspaceRelativeSlashPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "WorkspaceRoot");
        var normalizer = CreateNormalizer();
        var target = new WorkspacePathService(root, normalizer.Object);

        var relativeResult = target.TryNormalizePath(Path.Combine("Folder", "Document.cs"), out var relativePath);
        var absoluteResult = target.TryNormalizePath(Path.Combine(root, "Project", "Project.csproj"), out var absolutePath);

        relativeResult.Should().BeTrue();
        relativePath.Should().Be("Folder/Document.cs");
        absoluteResult.Should().BeTrue();
        absolutePath.Should().Be("Project/Project.csproj");
    }

    [Fact]
    public void GIVEN_WhitespacePath_WHEN_Normalizing_THEN_ShouldReturnFalseAndNullWithoutCallingNormalizer()
    {
        var normalizer = new Mock<IWorkspacePathNormalizer>();
        var target = new WorkspacePathService("WorkspaceRoot", normalizer.Object);

        var result = target.TryNormalizePath("   ", out var normalizedPath);

        result.Should().BeFalse();
        normalizedPath.Should().BeNull();
        normalizer.VerifyNoOtherCalls();
    }

    [Fact]
    public void GIVEN_PathNormalizerRejectsPath_WHEN_Normalizing_THEN_ShouldReturnFalseAndNull()
    {
        var normalizer = new Mock<IWorkspacePathNormalizer>();
        var rejectedPath = string.Empty;
        normalizer
            .Setup(item => item.TryGetWorkspaceRelativePath("WorkspaceRoot", "\0Document.cs", out rejectedPath))
            .Returns(false);
        var target = new WorkspacePathService("WorkspaceRoot", normalizer.Object);

        var result = target.TryNormalizePath("\0Document.cs", out var normalizedPath);

        result.Should().BeFalse();
        normalizedPath.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NoWorkspaceIdentity_WHEN_Normalizing_THEN_ShouldBindToEmptyWorkspaceRoot()
    {
        var normalizer = CreateNormalizer();
        var factory = new WorkspacePathServiceFactory(normalizer.Object);
        var target = factory.Create(workspaceIdentity: null);
        var relativePath = Path.Combine("Folder", "Document.cs");

        var result = target.TryNormalizePath(relativePath, out var normalizedPath);

        result.Should().BeTrue();
        normalizedPath.Should().Be(Path.GetFullPath(relativePath).Replace(Path.DirectorySeparatorChar, '/'));
    }

    [Fact]
    public void GIVEN_WorkspaceIdentity_WHEN_CreatingPathService_THEN_ShouldBindToIdentityWorkspaceRoot()
    {
        var normalizedPath = "Document.cs";
        var normalizer = new Mock<IWorkspacePathNormalizer>();
        normalizer
            .Setup(item => item.TryGetWorkspaceRelativePath("WorkspaceRoot", "Document.cs", out normalizedPath))
            .Returns(true);
        var factory = new WorkspacePathServiceFactory(normalizer.Object);
        var identity = new WorkspaceIdentity
        {
            WorkspaceRoot = "WorkspaceRoot",
        };
        var target = factory.Create(identity);

        var result = target.TryNormalizePath("Document.cs", out var resultPath);

        result.Should().BeTrue();
        resultPath.Should().Be("Document.cs");
        normalizer.Verify(
            item => item.TryGetWorkspaceRelativePath("WorkspaceRoot", "Document.cs", out normalizedPath),
            Times.Once);
    }

    private static Mock<IWorkspacePathNormalizer> CreateNormalizer()
    {
        var normalizer = new Mock<IWorkspacePathNormalizer>();
        normalizer
            .Setup(item => item.TryGetWorkspaceRelativePath(
                It.IsAny<string>(),
                It.IsAny<string>(),
                out It.Ref<string>.IsAny))
            .Returns((string workspaceRoot, string path, out string relativePath) =>
            {
                var fullPath = string.IsNullOrWhiteSpace(workspaceRoot)
                    ? Path.GetFullPath(path)
                    : Path.GetFullPath(path, workspaceRoot);

                relativePath = string.IsNullOrWhiteSpace(workspaceRoot)
                    ? fullPath.Replace(Path.DirectorySeparatorChar, '/')
                    : Path.GetRelativePath(workspaceRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
                return true;
            });

        return normalizer;
    }
}
