using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FindSymbols;

using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceLoadWorkflowTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceLoader> _workspaceLoader;
    private readonly Mock<IWorkspaceRootResolver> _workspaceRootResolver;
    private readonly WorkspaceLoadWorkflow _target;

    public WorkspaceLoadWorkflowTests()
    {
        _workspace = new AdhocWorkspace();
        _workspaceLoader = new Mock<IWorkspaceLoader>();
        _workspaceRootResolver = new Mock<IWorkspaceRootResolver>();
        _workspaceRootResolver.Setup(item => item.Contains(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _target = new WorkspaceLoadWorkflow(_workspaceLoader.Object, _workspaceRootResolver.Object);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_Loading_THEN_ShouldPropagateBeforeInspectingOrLoading()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.LoadAsync("/workspace/Project.csproj", "/workspace", null, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _workspaceLoader.Verify(item => item.InspectCompatibility(It.IsAny<string>(), It.IsAny<WorkspaceMsBuildProperties?>()), Times.Never);
        _workspaceLoader.Verify(item => item.LoadAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_ProjectPreflightFails_WHEN_Loading_THEN_ShouldReturnExpectedFailure(bool hasDiagnostics)
    {
        var diagnostics = hasDiagnostics ? new[] { new DiagnosticInfo { Message = "Message" } } : [];
        _workspaceLoader.Setup(item => item.InspectCompatibility("/workspace/Project.csproj", null))
            .Returns((false, diagnostics));

        var result = await _target.LoadAsync(
            "/workspace/Project.csproj",
            "/workspace",
            null,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeTrue();
        result.Failure.Should().Be(hasDiagnostics
            ? ValidatedWorkspaceLoadFailure.LoadFailed
            : ValidatedWorkspaceLoadFailure.NotSupported);

        result.Diagnostics.Should().Equal(diagnostics);
        _workspaceLoader.Verify(item => item.LoadAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_IncompleteLoaderResult_WHEN_Loading_THEN_ShouldDisposeAvailableWorkspaceAndReturnFailure(
        bool hasSolution)
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var diagnostics = new[] { new DiagnosticInfo { Message = "Message" } };
        _workspaceLoader.Setup(item => item.LoadAsync("/workspace/Solution.sln", null, TestContext.Current.CancellationToken))
            .ReturnsAsync(new WorkspaceLoadResult
            {
                Solution = hasSolution ? _workspace.CurrentSolution : null,
                ProjectTargetFrameworks = WorkspaceProjectTargetFrameworkMap.Empty,
                Workspace = hasSolution ? null : loadedWorkspace.Object,
                Diagnostics = diagnostics,
            });

        var result = await _target.LoadAsync(
            "/workspace/Solution.sln",
            "/workspace",
            null,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeTrue();
        result.Failure.Should().Be(ValidatedWorkspaceLoadFailure.LoadFailed);
        result.Diagnostics.Should().Equal(diagnostics);
        Times expectedDisposals;
        if (hasSolution)
        {
            expectedDisposals = Times.Never();
        }
        else
        {
            expectedDisposals = Times.Once();
        }

        loadedWorkspace.Verify(item => item.Dispose(), expectedDisposals);
    }

    [Fact]
    public async Task GIVEN_LoaderResultWithoutTargetFrameworkMap_WHEN_Loading_THEN_ShouldDisposeWorkspaceAndReturnFailure()
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        _workspaceLoader.Setup(item => item.LoadAsync("/workspace/Solution.sln", null, TestContext.Current.CancellationToken))
            .ReturnsAsync(new WorkspaceLoadResult
            {
                Solution = _workspace.CurrentSolution,
                Workspace = loadedWorkspace.Object,
            });

        var result = await _target.LoadAsync(
            "/workspace/Solution.sln",
            "/workspace",
            null,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeTrue();
        result.Failure.Should().Be(ValidatedWorkspaceLoadFailure.LoadFailed);
        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_LoadedProjectOutsideRoot_WHEN_Loading_THEN_ShouldDisposeAndRejectIt()
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var solution = CreateSolution("/outside/Project.csproj", "/outside/Document.cs");
        SetupLoadedWorkspace("/workspace/Solution.sln", solution, loadedWorkspace);
        _workspaceLoader.Setup(item => item.InspectCompatibility("/outside/Project.csproj", null))
            .Returns((true, []));

        _workspaceRootResolver.Setup(item => item.Contains("/workspace", "/outside/Project.csproj")).Returns(false);

        var result = await _target.LoadAsync(
            "/workspace/Solution.sln",
            "/workspace",
            null,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeTrue();
        result.Failure.Should().Be(ValidatedWorkspaceLoadFailure.OutsideWorkspaceRoot);
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "WorkspaceInputOutsideRoot"
            && diagnostic.Message.Contains("/outside/Project.csproj", StringComparison.Ordinal)
            && diagnostic.Message.Contains("/workspace", StringComparison.Ordinal));

        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_EvaluatedDocumentOutsideRoot_WHEN_Loading_THEN_ShouldAcceptItAsReadOnlyInput()
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var solution = CreateSolution("/workspace/Project.csproj", "/external/Linked.cs");

        _workspaceLoader
            .Setup(item => item.LoadAsync("/workspace/Solution.sln", null, TestContext.Current.CancellationToken))
            .ReturnsAsync(new WorkspaceLoadResult
            {
                Workspace = loadedWorkspace.Object,
                Solution = solution,
                ProjectTargetFrameworks = WorkspaceProjectTargetFrameworkMap.Empty,
            });

        _workspaceLoader.Setup(item => item.InspectCompatibility("/workspace/Project.csproj", null))
            .Returns((true, []));

        var result = await _target.LoadAsync(
            "/workspace/Solution.sln",
            "/workspace",
            null,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeFalse();
        result.Solution.Should().BeSameAs(solution);
        loadedWorkspace.Verify(item => item.Dispose(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ProjectWithinConfiguredArtifactsPath_WHEN_Loading_THEN_ShouldRejectIt()
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var solution = CreateSolution("/artifacts/Project.csproj", "/artifacts/Document.cs");
        var properties = new WorkspaceMsBuildProperties
        {
            ArtifactsPath = "/artifacts",
        };

        _workspaceLoader
            .Setup(item => item.LoadAsync("/workspace/Solution.sln", properties, TestContext.Current.CancellationToken))
            .ReturnsAsync(new WorkspaceLoadResult
            {
                Workspace = loadedWorkspace.Object,
                Solution = solution,
                ProjectTargetFrameworks = WorkspaceProjectTargetFrameworkMap.Empty,
            });

        _workspaceLoader.Setup(item => item.InspectCompatibility("/artifacts/Project.csproj", properties))
            .Returns((true, []));
        _workspaceRootResolver.Setup(item => item.Contains("/workspace", "/artifacts/Project.csproj")).Returns(false);

        var result = await _target.LoadAsync(
            "/workspace/Solution.sln",
            "/workspace",
            properties,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeTrue();
        result.Failure.Should().Be(ValidatedWorkspaceLoadFailure.OutsideWorkspaceRoot);
        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_LoadedProjectCompatibilityFails_WHEN_Loading_THEN_ShouldDisposeAndReturnExpectedFailure(
        bool hasDiagnostics)
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var solution = CreateSolution("/workspace/Project.csproj", "/workspace/Document.cs");
        var diagnostics = hasDiagnostics ? new[] { new DiagnosticInfo { Message = "Message" } } : [];
        SetupLoadedWorkspace("/workspace/Solution.sln", solution, loadedWorkspace);
        _workspaceLoader.Setup(item => item.InspectCompatibility("/workspace/Project.csproj", null))
            .Returns((false, diagnostics));

        var result = await _target.LoadAsync(
            "/workspace/Solution.sln",
            "/workspace",
            null,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeTrue();
        result.Failure.Should().Be(hasDiagnostics
            ? ValidatedWorkspaceLoadFailure.LoadFailed
            : ValidatedWorkspaceLoadFailure.NotSupported);

        if (hasDiagnostics)
        {
            result.Diagnostics.Should().Equal(diagnostics);
        }
        else
        {
            result.Diagnostics.Should().ContainSingle(item => item.Id == "WorkspaceProjectSkipped");
        }

        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CancellationAfterLoading_WHEN_InspectingProjects_THEN_ShouldDisposeAndPropagate()
    {
        using var cancellationSource = new CancellationTokenSource();
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var solution = CreateSolution("/workspace/Project.csproj", "/workspace/Document.cs");
        SetupLoadedWorkspace("/workspace/Solution.sln", solution, loadedWorkspace, cancellationSource.Token);
        _workspaceLoader.Setup(item => item.InspectCompatibility("/workspace/Project.csproj", null))
            .Returns((true, []));

        _workspaceRootResolver
            .Setup(item => item.Contains("/workspace", It.IsAny<string>()))
            .Returns(() =>
            {
                cancellationSource.Cancel();
                return true;
            });

        var action = async () => await _target.LoadAsync(
            "/workspace/Solution.sln",
            "/workspace",
            null,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ValidationThrowsAfterLoading_WHEN_Loading_THEN_ShouldDisposeAndPropagate()
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var solution = CreateSolution("/workspace/Project.csproj", "/workspace/Document.cs");
        SetupLoadedWorkspace("/workspace/Solution.sln", solution, loadedWorkspace);
        _workspaceLoader.Setup(item => item.InspectCompatibility("/workspace/Project.csproj", null))
            .Returns((true, []));

        _workspaceRootResolver.Setup(item => item.Contains("/workspace", It.IsAny<string>()))
            .Throws<InvalidOperationException>();

        var action = async () => await _target.LoadAsync(
            "/workspace/Solution.sln",
            "/workspace",
            null,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ValidProjectLoad_WHEN_Loading_THEN_ShouldReturnLoadedWorkspaceAndDiagnostics()
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var solution = CreateSolution("/workspace/Project.csproj", "/workspace/Document.cs");
        var targetFrameworkMappings = new Dictionary<ProjectId, string>
        {
            [solution.ProjectIds.Single()] = "net10.0",
        };
        var targetFrameworks = new WorkspaceProjectTargetFrameworkMap(targetFrameworkMappings);
        var diagnostics = new[] { new DiagnosticInfo { Message = "Message" } };
        _workspaceLoader.SetupSequence(item => item.InspectCompatibility("/workspace/Project.csproj", null))
            .Returns((true, []))
            .Returns((true, []));

        _workspaceLoader.Setup(item => item.LoadAsync("/workspace/Project.csproj", null, TestContext.Current.CancellationToken))
            .ReturnsAsync(new WorkspaceLoadResult
            {
                Workspace = loadedWorkspace.Object,
                Solution = solution,
                ProjectTargetFrameworks = targetFrameworks,
                Diagnostics = diagnostics,
            });

        var result = await _target.LoadAsync(
            "/workspace/Project.csproj",
            "/workspace",
            null,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeFalse();
        result.Failure.Should().BeNull();
        result.Workspace.Should().BeSameAs(loadedWorkspace.Object);
        result.Solution.Should().BeSameAs(solution);
        result.ProjectTargetFrameworks.Should().BeSameAs(targetFrameworks);
        result.Diagnostics.Should().Equal(diagnostics);
        loadedWorkspace.Verify(item => item.Dispose(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_MixedSupportedAndUnsupportedProjects_WHEN_Loading_THEN_ShouldRetainOnlySupportedProjects()
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var solution = CreateSolution("/workspace/Project.csproj", "/workspace/Document.cs")
            .AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Default,
                "VisualBasicProject",
                "VisualBasicProject",
                LanguageNames.VisualBasic,
                filePath: "/workspace/VisualBasicProject.vbproj"))
            .AddProject("PathlessProject", "PathlessProject", LanguageNames.CSharp).Solution;

        SetupLoadedWorkspace("/workspace/Solution.sln", solution, loadedWorkspace);
        _workspaceLoader.Setup(item => item.InspectCompatibility("/workspace/Project.csproj", null))
            .Returns((true, []));

        var result = await _target.LoadAsync(
            "/workspace/Solution.sln",
            "/workspace",
            null,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeFalse();
        result.Solution!.Projects.Should().ContainSingle(item => item.Name == "Project");
        result.Diagnostics.Should().HaveCount(2);
        result.Diagnostics.Should().OnlyContain(item => item.Id == "WorkspaceProjectSkipped");
        _workspaceLoader.Verify(item => item.InspectCompatibility("/workspace/Project.csproj", null), Times.Once);
    }

    [Fact]
    public async Task GIVEN_UnresolvedAnalyzerReferences_WHEN_Loading_THEN_ShouldRemoveThemAndRetainActionableDiagnostics()
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var projectAnalyzer = new UnresolvedAnalyzerReference("/workspace/missing-project-analyzer.dll");
        var solutionAnalyzer = new UnresolvedAnalyzerReference("/workspace/missing-solution-analyzer.dll");
        var solution = CreateSolution(
                "/workspace/Project.csproj",
                "/workspace/Document.cs",
                "public class C { } public class D : C { }")
            .AddAnalyzerReference(solutionAnalyzer);

        var projectId = solution.ProjectIds.Single();
        solution = solution.AddAnalyzerReference(projectId, projectAnalyzer);
        SetupLoadedWorkspace("/workspace/Solution.sln", solution, loadedWorkspace);
        _workspaceLoader.Setup(item => item.InspectCompatibility("/workspace/Project.csproj", null))
            .Returns((true, []));

        var result = await _target.LoadAsync(
            "/workspace/Solution.sln",
            "/workspace",
            null,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeFalse();
        var effectiveSolution = result.Solution.Should().BeAssignableTo<Solution>().Which;
        effectiveSolution.AnalyzerReferences.Should().NotContain(item => item is UnresolvedAnalyzerReference);
        var project = effectiveSolution.Projects.Single();
        project.AnalyzerReferences.Should().NotContain(item => item is UnresolvedAnalyzerReference);
        result.Diagnostics.Should().ContainSingle(item =>
            item.Id == "WorkspaceAnalyzerReferenceSkipped"
            && item.Message.Contains("missing-solution-analyzer.dll", StringComparison.Ordinal)
            && item.Message.Contains("the solution", StringComparison.Ordinal));

        result.Diagnostics.Should().ContainSingle(item =>
            item.Id == "WorkspaceAnalyzerReferenceSkipped"
            && item.Message.Contains("missing-project-analyzer.dll", StringComparison.Ordinal)
            && item.Message.Contains("project 'Project'", StringComparison.Ordinal));

        var compilationResult = await project.GetCompilationAsync(TestContext.Current.CancellationToken);
        var compilation = compilationResult.Should().BeAssignableTo<Compilation>().Which;
        var symbol = compilation.GetTypeByMetadataName("C").Should().BeAssignableTo<INamedTypeSymbol>().Which;
        var referencedSymbols = await SymbolFinder.FindReferencesAsync(
            symbol,
            effectiveSolution,
            TestContext.Current.CancellationToken);

        referencedSymbols.SelectMany(item => item.Locations).Should().ContainSingle();
        loadedWorkspace.Verify(item => item.Dispose(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_OnlyUnsupportedLanguageProjects_WHEN_Loading_THEN_ShouldDisposeAndRejectWorkspace()
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var solution = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "VisualBasicProject",
            "VisualBasicProject",
            LanguageNames.VisualBasic,
            filePath: "/workspace/VisualBasicProject.vbproj"));

        SetupLoadedWorkspace("/workspace/Solution.sln", solution, loadedWorkspace);

        var result = await _target.LoadAsync(
            "/workspace/Solution.sln",
            "/workspace",
            null,
            TestContext.Current.CancellationToken);

        result.HasFailure.Should().BeTrue();
        result.Failure.Should().Be(ValidatedWorkspaceLoadFailure.NotSupported);
        result.Diagnostics.Should().ContainSingle(item => item.Id == "WorkspaceProjectSkipped");
        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
        _workspaceLoader.Verify(item => item.InspectCompatibility(It.IsAny<string>(), It.IsAny<WorkspaceMsBuildProperties?>()), Times.Never);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private void SetupLoadedWorkspace(
        string path,
        Solution solution,
        Mock<ILoadedWorkspace> loadedWorkspace,
        CancellationToken? cancellationToken = null)
    {
        _workspaceLoader.Setup(item => item.LoadAsync(path, null, cancellationToken ?? TestContext.Current.CancellationToken))
            .ReturnsAsync(new WorkspaceLoadResult
            {
                Workspace = loadedWorkspace.Object,
                Solution = solution,
                ProjectTargetFrameworks = WorkspaceProjectTargetFrameworkMap.Empty,
            });
    }

    private Solution CreateSolution(
        string projectPath,
        string documentPath,
        string source = "class C { }")
    {
        var projectId = ProjectId.CreateNewId();
        return _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Default,
                "Project",
                "Project",
                LanguageNames.CSharp,
                filePath: projectPath))
            .AddDocument(DocumentId.CreateNewId(projectId), "Document.cs", SourceText.From(source), filePath: documentPath);
    }
}
