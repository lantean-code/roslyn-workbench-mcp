namespace Roslyn.Workbench.Mcp.Workspace.Test.Projects;

public sealed class ProjectStructureSolutionHierarchyIntegrationTests
{
    [Fact]
    public async Task GIVEN_MissingSolutionPath_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnEmpty()
    {
        var target = new ProjectStructureService();

        var result = await target.GetSolutionHierarchyAsync(null, TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeTrue();
        result.Folders.Should().BeEmpty();
        result.ProjectFolderPaths.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_UnsupportedSolutionExtension_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnEmpty()
    {
        var target = new ProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.txt");
            await File.WriteAllTextAsync(solutionPath, "content", TestContext.Current.CancellationToken);

            var result = await target.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

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
        var target = new ProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, "<Solution>", TestContext.Current.CancellationToken);

            var result = await target.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

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
        var target = new ProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.sln");
            await File.WriteAllTextAsync(solutionPath, CreateSlnContent().Replace("\n", Environment.NewLine, StringComparison.Ordinal), TestContext.Current.CancellationToken);

            var result = await target.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

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
        var target = new ProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, CreateSlnxContent().Replace("\n", Environment.NewLine, StringComparison.Ordinal), TestContext.Current.CancellationToken);

            var result = await target.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

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
    public async Task GIVEN_MissingSolutionFile_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFailure()
    {
        var target = new ProjectStructureService();
        var directoryPath = CreateDirectoryPath();
        var solutionPath = Path.Combine(directoryPath, "Missing.slnx");

        try
        {
            var result = await target.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

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
        var target = new ProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, CreateSlnxContent(), TestContext.Current.CancellationToken);
            using var cancellationSource = new CancellationTokenSource();
            await cancellationSource.CancelAsync();

            var action = async () => await target.GetSolutionHierarchyAsync(solutionPath, cancellationSource.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
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
