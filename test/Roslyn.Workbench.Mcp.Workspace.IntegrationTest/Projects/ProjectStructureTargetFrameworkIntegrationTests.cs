using Moq;

using Roslyn.Workbench.Mcp.Workspace.State;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Projects;

public sealed class ProjectStructureTargetFrameworkIntegrationTests
{
    private static readonly Guid _workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void GIVEN_TargetFrameworksImportedFromProps_WHEN_GettingTargetFrameworks_THEN_ShouldReturnEvaluatedValues()
    {
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var buildDirectoryPath = Path.Combine(directoryPath, "build");
            Directory.CreateDirectory(buildDirectoryPath);

            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            var importedPropsPath = Path.Combine(buildDirectoryPath, "Frameworks.props");
            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="build\Frameworks.props" />
                </Project>
                """);

            File.WriteAllText(importedPropsPath, """
                <Project>
                  <PropertyGroup>
                    <TargetFrameworks>net10.0;net9.0</TargetFrameworks>
                  </PropertyGroup>
                </Project>
                """);

            var result = target.GetTargetFrameworks(_workspaceId, projectPath);

            result.IsSucceeded.Should().BeTrue();
            result.TargetFrameworks.Should().Equal("net10.0", "net9.0");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_MissingProjectFile_WHEN_GettingTargetFrameworks_THEN_ShouldReturnFailure()
    {
        var target = CreateTarget();
        var projectPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "Missing.csproj");

        var result = target.GetTargetFrameworks(_workspaceId, projectPath);

        result.IsSucceeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain(projectPath);
    }

    [Fact]
    public void GIVEN_MalformedProject_WHEN_GettingTargetFrameworks_THEN_ShouldReturnFailure()
    {
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Malformed.csproj");
            File.WriteAllText(projectPath, "<Project><PropertyGroup>");

            var result = target.GetTargetFrameworks(_workspaceId, projectPath);

            result.IsSucceeded.Should().BeFalse();
            result.ErrorMessage.Should().Contain(projectPath);
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_ProjectWithoutTargetFramework_WHEN_GettingTargetFrameworks_THEN_ShouldReturnSuccessfulEmptyResult()
    {
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            File.WriteAllText(projectPath, "<Project />");

            var result = target.GetTargetFrameworks(_workspaceId, projectPath);

            result.IsSucceeded.Should().BeTrue();
            result.TargetFrameworks.Should().BeEmpty();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_ProjectWithSingleTargetFramework_WHEN_GettingTargetFrameworks_THEN_ShouldReturnEvaluatedValue()
    {
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            File.WriteAllText(projectPath, "<Project><PropertyGroup><TargetFramework> net10.0 </TargetFramework></PropertyGroup></Project>");

            var result = target.GetTargetFrameworks(_workspaceId, projectPath);

            result.IsSucceeded.Should().BeTrue();
            result.TargetFrameworks.Should().ContainSingle().Which.Should().Be("net10.0");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_ConfigurationSpecificTargetFramework_WHEN_GettingTargetFrameworks_THEN_ShouldUseWorkspaceProperties()
    {
        var properties = new WorkspaceMsBuildProperties
        {
            Configuration = "Release",
        };

        var target = CreateTarget(properties);
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Configured.csproj");
            File.WriteAllText(projectPath, """
                <Project>
                  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                  <PropertyGroup Condition="'$(Configuration)' != 'Release'">
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            var result = target.GetTargetFrameworks(_workspaceId, projectPath);

            result.IsSucceeded.Should().BeTrue();
            result.TargetFrameworks.Should().ContainSingle().Which.Should().Be("net9.0");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_ProjectWithoutFilePath_WHEN_GettingTargetFrameworks_THEN_ShouldReturnSuccessfulEmptyResult()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Project", LanguageNames.CSharp);
        var target = CreateTarget();

        var result = target.GetTargetFrameworks(_workspaceId, project);

        result.IsSucceeded.Should().BeTrue();
        result.TargetFrameworks.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_ProjectBatchContainsDuplicateAndMissingPaths_WHEN_GettingTargetFrameworks_THEN_ShouldPreserveInputOrder()
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var sharedProjectPath = Path.Combine(directoryPath, "Shared.csproj");
            var otherProjectPath = Path.Combine(directoryPath, "Other.csproj");
            File.WriteAllText(sharedProjectPath, "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            File.WriteAllText(otherProjectPath, "<Project><PropertyGroup><TargetFrameworks>net9.0;net8.0</TargetFrameworks></PropertyGroup></Project>");

            var sharedProjectA = AddProject(workspace, "SharedA", sharedProjectPath);
            var pathlessProject = AddProject(workspace, "Pathless", filePath: null);
            var sharedProjectB = AddProject(workspace, "SharedB", sharedProjectPath);
            var otherProject = AddProject(workspace, "Other", otherProjectPath);

            Project[] projects =
            [
                sharedProjectA,
                pathlessProject,
                sharedProjectB,
                otherProject,
            ];

            var results = target.GetTargetFrameworks(
                _workspaceId,
                projects,
                TestContext.Current.CancellationToken);

            results.Should().HaveCount(4);
            results[0].TargetFrameworks.Should().Equal("net10.0");
            results[1].TargetFrameworks.Should().BeEmpty();
            results[2].Should().BeSameAs(results[0]);
            results[3].TargetFrameworks.Should().Equal("net9.0", "net8.0");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_CaseVariantProjectPathsOnCaseInsensitiveFileSystem_WHEN_GettingTargetFrameworks_THEN_ShouldReuseEvaluation()
    {
        using var workspace = new AdhocWorkspace();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            var caseVariantProjectPath = Path.Combine(directoryPath, "SAMPLE.CSPROJ");
            File.WriteAllText(projectPath, "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

            var firstProject = AddProject(workspace, "First", projectPath);
            var secondProject = AddProject(workspace, "Second", caseVariantProjectPath);
            Project[] projects = [firstProject, secondProject];

            var fileSystem = new FileSystem();
            var pathComparison = new Mock<IWorkspacePathComparison>();
            pathComparison
                .Setup(item => item.CreateKey(It.IsAny<string>()))
                .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: false));

            var pathNormalizer = new WorkspacePathNormalizer(fileSystem);
            var msBuildPropertiesProvider = new Mock<IWorkspaceMsBuildPropertiesProvider>();
            var target = new ProjectStructureService(pathComparison.Object, pathNormalizer, msBuildPropertiesProvider.Object);

            var results = target.GetTargetFrameworks(
                _workspaceId,
                projects,
                TestContext.Current.CancellationToken);

            results.Should().HaveCount(2);
            results[0].TargetFrameworks.Should().ContainSingle().Which.Should().Be("net10.0");
            results[1].Should().BeSameAs(results[0]);
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_CancelledToken_WHEN_GettingBatchTargetFrameworks_THEN_ShouldCancelBeforeEvaluation()
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget();
        var project = AddProject(workspace, "Project", "Project.csproj");
        Project[] projects = [project];
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        cancellationSource.Cancel();

        var action = () => target.GetTargetFrameworks(_workspaceId, projects, cancellationToken);

        action.Should().Throw<OperationCanceledException>()
            .Where(exception => exception.CancellationToken == cancellationToken);
    }

    private static Project AddProject(AdhocWorkspace workspace, string name, string? filePath)
    {
        var projectInfo = Microsoft.CodeAnalysis.ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            name,
            name,
            LanguageNames.CSharp,
            filePath: filePath);

        return workspace.AddProject(projectInfo);
    }

    private static ProjectStructureService CreateTarget(WorkspaceMsBuildProperties? properties = null)
    {
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var pathNormalizer = new WorkspacePathNormalizer(fileSystem);
        var msBuildPropertiesProvider = new Mock<IWorkspaceMsBuildPropertiesProvider>();
        msBuildPropertiesProvider.Setup(item => item.Get(_workspaceId)).Returns(properties);

        return new ProjectStructureService(pathComparison, pathNormalizer, msBuildPropertiesProvider.Object);
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
}
