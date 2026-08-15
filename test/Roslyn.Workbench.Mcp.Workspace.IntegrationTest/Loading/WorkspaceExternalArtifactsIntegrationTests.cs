using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis.MSBuild;
using Moq;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceExternalArtifactsIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "WorkspaceLoader transfers the MSBuildWorkspace into the returned ILoadedWorkspace, which this test disposes after inspecting the loaded solution.")]
    public async Task GIVEN_ExternalEvaluatedSources_WHEN_LoadingProject_THEN_ShouldAcceptReadOnlyDocumentsAndCompile()
    {
        MsBuildTestRegistration.EnsureRegistered();
        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-external-artifacts-tests");
        var workspaceRoot = Path.Combine(directory.DirectoryPath, "workspace");
        var artifactsPath = Path.Combine(directory.DirectoryPath, "artifacts");
        var packageRoot = Path.Combine(directory.DirectoryPath, "packages", "Synthetic.Package");
        var packageBuildRoot = Path.Combine(packageRoot, "buildTransitive");
        var packageContentRoot = Path.Combine(packageRoot, "_content");
        var sharedRoot = Path.Combine(directory.DirectoryPath, "shared");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(artifactsPath);
        Directory.CreateDirectory(packageBuildRoot);
        Directory.CreateDirectory(packageContentRoot);
        Directory.CreateDirectory(sharedRoot);

        var projectPath = Path.Combine(workspaceRoot, "Sample.csproj");
        var documentPath = Path.Combine(workspaceRoot, "Sample.cs");
        var packageTargetsPath = Path.Combine(packageBuildRoot, "Synthetic.Package.targets");
        var packageSourcePath = Path.Combine(packageContentRoot, "PackageSource.cs");
        var linkedSourcePath = Path.Combine(sharedRoot, "LinkedSource.cs");
        var packageTargetsImport = GetProjectRelativePath(workspaceRoot, packageTargetsPath);
        var linkedSourceInclude = GetProjectRelativePath(workspaceRoot, linkedSourcePath);
        var cancellationToken = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(packageSourcePath, "public sealed class PackageSource { }", cancellationToken);
        await File.WriteAllTextAsync(linkedSourcePath, "public sealed class LinkedSource { }", cancellationToken);
        await File.WriteAllTextAsync(packageTargetsPath, """
            <Project>
              <ItemGroup>
                <Compile Include="$(MSBuildThisFileDirectory)../_content/PackageSource.cs" Link="PackageSource.cs" />
              </ItemGroup>
            </Project>
            """, cancellationToken);

        await File.WriteAllTextAsync(projectPath, $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <Import Project="Missing.props" Condition="'$(ArtifactsPath)' == ''" />
              <Import Project="{{packageTargetsImport}}" />
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="{{linkedSourceInclude}}" Link="LinkedSource.cs" />
              </ItemGroup>
            </Project>
            """, cancellationToken);

        await File.WriteAllTextAsync(documentPath, "public sealed class Sample { public List<int> Values { get; } = []; }", cancellationToken);
        await RestoreAsync(projectPath, artifactsPath, cancellationToken);

        var properties = new WorkspaceMsBuildProperties
        {
            ArtifactsPath = artifactsPath,
        };

        var globalProperties = properties.ToGlobalProperties();
        var workspaceFactory = new Mock<IMsBuildWorkspaceFactory>();
        workspaceFactory
            .Setup(item => item.Create(It.Is<IReadOnlyDictionary<string, string>>(values =>
                values.Count == 1 && values["ArtifactsPath"] == artifactsPath)))
            .Returns(MSBuildWorkspace.Create(new Dictionary<string, string>(globalProperties, StringComparer.OrdinalIgnoreCase)));

        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var pathNormalizer = new WorkspacePathNormalizer(fileSystem);
        var pathContainment = new PhysicalPathContainment(fileSystem, pathComparison);
        var rootResolver = new WorkspaceRootResolver(fileSystem, pathComparison, pathContainment, pathNormalizer);
        var compatibilityInspector = new WorkspaceProjectCompatibilityInspector();
        var loader = new WorkspaceLoader(workspaceFactory.Object, compatibilityInspector, pathNormalizer);
        var target = new WorkspaceLoadWorkflow(loader, rootResolver);

        var result = await target.LoadAsync(
            projectPath,
            workspaceRoot,
            properties,
            cancellationToken);

        result.HasFailure.Should().BeFalse();
        using var loadedWorkspace = result.Workspace.Should().BeAssignableTo<ILoadedWorkspace>().Which;
        var project = result.Solution!.Projects.Should().ContainSingle().Which;
        project.Documents.Should().Contain(document =>
            document.FilePath != null
            && Path.GetFileName(document.FilePath).EndsWith("GlobalUsings.g.cs", StringComparison.Ordinal)
            && Path.GetFullPath(document.FilePath).StartsWith(Path.GetFullPath(artifactsPath), StringComparison.Ordinal));
        project.Documents.Should().Contain(document => PathEquals(document.FilePath, packageSourcePath));
        project.Documents.Should().Contain(document => PathEquals(document.FilePath, linkedSourcePath));

        var readOnlyDocumentValidator = new WorkspaceReadOnlyDocumentValidator(
            fileSystem,
            pathContainment,
            pathComparison);

        var validation = await readOnlyDocumentValidator.ValidateAsync(
            result.Solution,
            workspaceRoot,
            cancellationToken);

        validation.Should().Be(WorkspaceReadOnlyDocumentValidationStatus.Valid);

        var compilation = await project.GetCompilationAsync(cancellationToken);
        compilation.Should().NotBeNull();
        compilation!.GetDiagnostics(cancellationToken).Should().NotContain(diagnostic => diagnostic.Id == "CS0246");
        workspaceFactory.Verify(item => item.Create(It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Once);
    }

    private static string GetProjectRelativePath(string projectDirectory, string path)
    {
        return Path.GetRelativePath(projectDirectory, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool PathEquals(string? left, string right)
    {
        return left is not null
            && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.Ordinal);
    }

    private static async Task RestoreAsync(
        string projectPath,
        string artifactsPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("restore");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add($"--artifacts-path={artifactsPath}");
        startInfo.ArgumentList.Add("--nologo");

        using var process = Process.Start(startInfo);
        process.Should().NotBeNull();
        var standardOutputTask = process!.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        process.ExitCode.Should().Be(0, because: $"restore output was: {standardOutput}{Environment.NewLine}{standardError}");
    }
}
