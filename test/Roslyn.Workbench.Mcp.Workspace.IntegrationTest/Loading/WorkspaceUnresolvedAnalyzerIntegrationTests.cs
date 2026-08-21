using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Moq;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceUnresolvedAnalyzerIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "WorkspaceLoader transfers the MSBuildWorkspace into the returned ILoadedWorkspace, which this test disposes after the reference search.")]
    public async Task GIVEN_ProjectWithMissingAnalyzer_WHEN_LoadingAndFindingReferences_THEN_ShouldRetainDiagnosticAndCompleteSearch()
    {
        MsBuildTestRegistration.EnsureRegistered();
        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-unresolved-analyzer-tests");
        var projectPath = Path.Combine(directory.DirectoryPath, "Sample.csproj");
        var documentPath = Path.Combine(directory.DirectoryPath, "Sample.cs");
        var cancellationToken = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <Analyzer Include="missing-analyzer.dll" />
              </ItemGroup>
            </Project>
            """, cancellationToken);

        await File.WriteAllTextAsync(documentPath, "public class C { } public class D : C { }", cancellationToken);
        var workspaceFactory = new Mock<IMsBuildWorkspaceFactory>();
        workspaceFactory.Setup(item => item.Create(null)).Returns(MSBuildWorkspace.Create());
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var pathNormalizer = new WorkspacePathNormalizer(fileSystem);
        var pathContainment = new PhysicalPathContainment(fileSystem, pathComparison);
        var compatibilityInspector = new WorkspaceProjectCompatibilityInspector();
        var rootResolver = new WorkspaceRootResolver(fileSystem, pathComparison, pathContainment, pathNormalizer);
        var loader = new WorkspaceLoader(workspaceFactory.Object, compatibilityInspector, pathComparison, pathNormalizer);
        var target = new WorkspaceLoadWorkflow(loader, rootResolver);

        var result = await target.LoadAsync(
            projectPath,
            directory.DirectoryPath,
            null,
            cancellationToken);

        result.HasFailure.Should().BeFalse();
        using var loadedWorkspace = result.Workspace.Should().BeAssignableTo<ILoadedWorkspace>().Which;
        var solution = result.Solution.Should().BeAssignableTo<Solution>().Which;
        var project = solution.Projects.Should().ContainSingle().Which;
        project.AnalyzerReferences.Should().NotContain(item => item is UnresolvedAnalyzerReference);
        result.Diagnostics.Should().ContainSingle(item =>
            item.Id == "WorkspaceAnalyzerReferenceSkipped"
            && item.Severity == Results.DiagnosticSeverity.Warning
            && item.Message.Contains("missing-analyzer.dll", StringComparison.Ordinal));

        var compilationResult = await project.GetCompilationAsync(cancellationToken);
        var compilation = compilationResult.Should().BeAssignableTo<Compilation>().Which;
        var symbol = compilation.GetTypeByMetadataName("C").Should().BeAssignableTo<INamedTypeSymbol>().Which;
        var referencedSymbols = await SymbolFinder.FindReferencesAsync(
            symbol,
            solution,
            cancellationToken);

        referencedSymbols.SelectMany(item => item.Locations).Should().ContainSingle();
    }
}
