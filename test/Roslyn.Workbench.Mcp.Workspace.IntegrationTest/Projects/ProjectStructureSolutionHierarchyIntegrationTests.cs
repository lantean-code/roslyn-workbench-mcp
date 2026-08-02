using Moq;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Projects;

public sealed class ProjectStructureSolutionHierarchyIntegrationTests
{
    [Fact]
    public async Task GIVEN_MissingSolutionPath_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnEmpty()
    {
        var target = CreateTarget();

        var workspace = CreateWorkspaceIdentity(loadedPath: null);

        var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeTrue();
        result.Folders.Should().BeEmpty();
        result.ProjectFolderPaths.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_UnsupportedSolutionExtension_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnEmpty()
    {
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.txt");
            await File.WriteAllTextAsync(solutionPath, "content", TestContext.Current.CancellationToken);

            var workspace = CreateWorkspaceIdentity(solutionPath);

            var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeTrue();
            result.Folders.Should().BeEmpty();
            result.ProjectFolderPaths.Should().BeEmpty();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task GIVEN_InvalidSolutionContent_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFailure()
    {
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, "<Solution>", TestContext.Current.CancellationToken);

            var workspace = CreateWorkspaceIdentity(solutionPath);

            var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeFalse();
            result.ErrorMessage.Should().Contain(solutionPath);
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task GIVEN_SlnHierarchy_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFoldersAndProjectMembership()
    {
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.sln");
            await File.WriteAllTextAsync(solutionPath, CreateSlnContent().Replace("\n", Environment.NewLine, StringComparison.Ordinal), TestContext.Current.CancellationToken);

            var workspace = CreateWorkspaceIdentity(solutionPath);

            var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeTrue();
            result.Folders.Should().BeEquivalentTo(
            [
                new { Name = "src", Path = "src", ParentPath = (string?)null },
                new { Name = "core", Path = "src/core", ParentPath = (string?)"src" },
            ]);

            result.ProjectFolderPaths.Should().Contain(new KeyValuePair<string, string?>("Lib/Lib.csproj", "src/core"));
            result.ProjectFolderPaths.Should().Contain(new KeyValuePair<string, string?>("Root/Root.csproj", null));
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task GIVEN_SlnxHierarchy_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFoldersAndProjectMembership()
    {
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, CreateSlnxContent().Replace("\n", Environment.NewLine, StringComparison.Ordinal), TestContext.Current.CancellationToken);

            var workspace = CreateWorkspaceIdentity(solutionPath);

            var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeTrue();
            result.Folders.Should().BeEquivalentTo(
            [
                new { Name = "src", Path = "src", ParentPath = (string?)null },
                new { Name = "core", Path = "src/core", ParentPath = (string?)"src" },
            ]);

            result.ProjectFolderPaths.Should().Contain(new KeyValuePair<string, string?>("Lib/Lib.csproj", "src/core"));
            result.ProjectFolderPaths.Should().Contain(new KeyValuePair<string, string?>("Root/Root.csproj", null));
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_MalformedLoadedPath_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFailure()
    {
        var target = CreateTarget();
        var workspace = CreateWorkspaceIdentity("\0Sample.slnx", Path.GetTempPath());

        var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("workspace paths are invalid");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_MalformedWorkspaceRoot_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFailure()
    {
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, CreateSlnxContent(), TestContext.Current.CancellationToken);
            var workspace = CreateWorkspaceIdentity(solutionPath, "\0WorkspaceRoot");

            var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeFalse();
            result.ErrorMessage.Should().Contain("workspace paths are invalid");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Theory]
    [InlineData(".sln")]
    [InlineData(".slnx")]
    [Trait("Category", "Integration")]
    public async Task GIVEN_SolutionBelowWorkspaceRoot_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnWorkspaceRelativeProjectPaths(string extension)
    {
        var target = CreateTarget();
        var workspaceRoot = CreateDirectoryPath();

        try
        {
            var solutionDirectory = Path.Combine(workspaceRoot, "src", "Product");
            Directory.CreateDirectory(solutionDirectory);
            var solutionPath = Path.Combine(solutionDirectory, $"Sample{extension}");
            var solutionContent = extension == ".sln"
                ? CreateSlnContent()
                : CreateSlnxContent();

            await File.WriteAllTextAsync(
                solutionPath,
                solutionContent.Replace("\n", Environment.NewLine, StringComparison.Ordinal),
                TestContext.Current.CancellationToken);

            var workspace = CreateWorkspaceIdentity(solutionPath, workspaceRoot);

            var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeTrue();
            result.ProjectFolderPaths.Should().Contain(
                new KeyValuePair<string, string?>("src/Product/Lib/Lib.csproj", "src/core"));
            result.ProjectFolderPaths.Should().Contain(
                new KeyValuePair<string, string?>("src/Product/Root/Root.csproj", null));
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_CaseInsensitiveWorkspaceComparer_WHEN_GettingSolutionHierarchy_THEN_ShouldPreserveComparerForProjectLookup()
    {
        var pathComparison = new Mock<IWorkspacePathComparison>();
        var fileSystem = new FileSystem();
        var pathNormalizer = new WorkspacePathNormalizer(fileSystem);
        var target = new ProjectStructureService(pathComparison.Object, pathNormalizer);
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, CreateSlnxContent(), TestContext.Current.CancellationToken);
            pathComparison
                .Setup(item => item.GetComparer(directoryPath))
                .Returns(StringComparer.OrdinalIgnoreCase);

            var workspace = CreateWorkspaceIdentity(solutionPath, directoryPath);

            var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeTrue();
            result.ProjectFolderPaths.TryGetValue("lib/LIB.CSPROJ", out var folderPath).Should().BeTrue();
            folderPath.Should().Be("src/core");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ProjectPathCannotBeNormalized_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFailure(bool fullPathSucceeds)
    {
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var pathNormalizer = new Mock<IWorkspacePathNormalizer>();
        var target = new ProjectStructureService(pathComparison, pathNormalizer.Object);
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, CreateSlnxContent(), TestContext.Current.CancellationToken);
            var canonicalSolutionPath = solutionPath;
            var canonicalWorkspaceRoot = directoryPath;
            var fullProjectPath = Path.Combine(directoryPath, "Lib", "Lib.csproj");
            string failedPath = string.Empty;
            pathNormalizer.Setup(item => item.TryGetFullPath(solutionPath, out canonicalSolutionPath)).Returns(true);
            pathNormalizer.Setup(item => item.TryGetFullPath(directoryPath, out canonicalWorkspaceRoot)).Returns(true);
            pathNormalizer
                .Setup(item => item.TryGetFullPath("Lib/Lib.csproj", directoryPath, out fullProjectPath))
                .Returns(fullPathSucceeds);

            pathNormalizer
                .Setup(item => item.TryGetWorkspaceRelativePath(directoryPath, fullProjectPath, out failedPath))
                .Returns(false);

            var workspace = CreateWorkspaceIdentity(solutionPath, directoryPath);

            var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Could not normalize project path");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ProjectPathsNormalizeToSameIdentity_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnDuplicateFailure()
    {
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var pathNormalizer = new Mock<IWorkspacePathNormalizer>();
        var target = new ProjectStructureService(pathComparison, pathNormalizer.Object);
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, CreateSlnxContent(), TestContext.Current.CancellationToken);
            var canonicalSolutionPath = solutionPath;
            var canonicalWorkspaceRoot = directoryPath;
            var libFullPath = Path.Combine(directoryPath, "Lib", "Lib.csproj");
            var rootFullPath = Path.Combine(directoryPath, "Root", "Root.csproj");
            var duplicateProjectPath = "Duplicate.csproj";
            pathNormalizer.Setup(item => item.TryGetFullPath(solutionPath, out canonicalSolutionPath)).Returns(true);
            pathNormalizer.Setup(item => item.TryGetFullPath(directoryPath, out canonicalWorkspaceRoot)).Returns(true);
            pathNormalizer.Setup(item => item.TryGetFullPath("Lib/Lib.csproj", directoryPath, out libFullPath)).Returns(true);
            pathNormalizer.Setup(item => item.TryGetFullPath("Root/Root.csproj", directoryPath, out rootFullPath)).Returns(true);
            pathNormalizer
                .Setup(item => item.TryGetWorkspaceRelativePath(directoryPath, libFullPath, out duplicateProjectPath))
                .Returns(true);

            pathNormalizer
                .Setup(item => item.TryGetWorkspaceRelativePath(directoryPath, rootFullPath, out duplicateProjectPath))
                .Returns(true);

            var workspace = CreateWorkspaceIdentity(solutionPath, directoryPath);

            var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeFalse();
            result.ErrorMessage.Should().Contain("duplicate project path");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task GIVEN_MissingSolutionFile_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFailure()
    {
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();
        var solutionPath = Path.Combine(directoryPath, "Missing.slnx");

        try
        {
            var workspace = CreateWorkspaceIdentity(solutionPath);

            var result = await target.GetSolutionHierarchyAsync(workspace, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeFalse();
            result.ErrorMessage.Should().Contain(solutionPath);
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_GettingSolutionHierarchy_THEN_ShouldPropagateCancellation()
    {
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, CreateSlnxContent(), TestContext.Current.CancellationToken);
            using var cancellationSource = new CancellationTokenSource();
            await cancellationSource.CancelAsync();
            var workspace = CreateWorkspaceIdentity(solutionPath);

            var action = async () => await target.GetSolutionHierarchyAsync(workspace, cancellationSource.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    private static ProjectStructureService CreateTarget()
    {
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var pathNormalizer = new WorkspacePathNormalizer(fileSystem);

        return new ProjectStructureService(pathComparison, pathNormalizer);
    }

    private static WorkspaceIdentity CreateWorkspaceIdentity(string? loadedPath, string? workspaceRoot = null)
    {
        var effectiveLoadedPath = loadedPath ?? string.Empty;
        var effectiveWorkspaceRoot = workspaceRoot
            ?? Path.GetDirectoryName(effectiveLoadedPath)
            ?? string.Empty;

        return new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            LoadedPath = effectiveLoadedPath,
            WorkspaceRoot = effectiveWorkspaceRoot,
        };
    }

    private static string CreateDirectoryPath()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-project-structure-service-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static void DeleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static string CreateSlnContent()
    {
        return """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            VisualStudioVersion = 17.0.31903.59
            MinimumVisualStudioVersion = 10.0.40219.1
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "core", "core", "{22222222-2222-2222-2222-222222222222}"
            EndProject
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Lib", "Lib\Lib.csproj", "{33333333-3333-3333-3333-333333333333}"
            EndProject
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Root", "Root\Root.csproj", "{44444444-4444-4444-4444-444444444444}"
            EndProject
            Global
            	GlobalSection(NestedProjects) = preSolution
            		{22222222-2222-2222-2222-222222222222} = {11111111-1111-1111-1111-111111111111}
            		{33333333-3333-3333-3333-333333333333} = {22222222-2222-2222-2222-222222222222}
            	EndGlobalSection
            EndGlobal
            """;
    }

    private static string CreateSlnxContent()
    {
        return """
            <Solution>
              <Folder Name="/src/" />
              <Folder Name="/src/core/">
                <Project Path="Lib/Lib.csproj" />
              </Folder>
              <Project Path="Root/Root.csproj" />
            </Solution>
            """;
    }
}
