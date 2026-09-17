using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Configuration;
using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class TransactionCompilerValidationServiceTests
{
    private readonly Mock<IWorkspaceChangeDetector> _changeDetector = new();
    private readonly Mock<IWorkspaceQueryCacheScopeFactory> _cacheScopeFactory = new();
    private readonly Mock<IWorkspaceQueryCacheScope> _cacheScope = new();
    private readonly Mock<IWorkspaceResolverFactory> _resolverFactory = new();
    private readonly Mock<IWorkspaceResolver> _resolver = new();
    private readonly Mock<IWorkspacePathServiceFactory> _pathServiceFactory = new();
    private readonly Mock<IWorkspacePathService> _pathService = new();
    private readonly Mock<IWorkspacePathComparison> _pathComparison = new();
    private readonly TransactionCompilerValidationService _target;

    public TransactionCompilerValidationServiceTests()
    {
        _cacheScopeFactory
            .Setup(item => item.CreateScope(
                It.IsAny<Guid>(),
                It.IsAny<Solution>(),
                It.IsAny<string>()))
            .Returns(_cacheScope.Object);

        _cacheScope
            .Setup(item => item.GetOrCreateAsync<ProjectCompilerDiagnosticsCacheKey, ProjectCompilerDiagnostics>(
                It.IsAny<ProjectCompilerDiagnosticsCacheKey>(),
                It.IsAny<Func<CancellationToken, ValueTask<ProjectCompilerDiagnostics?>>>(),
                It.IsAny<Func<ProjectCompilerDiagnostics, long>>(),
                It.IsAny<Func<ProjectCompilerDiagnostics, bool>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                ProjectCompilerDiagnosticsCacheKey _,
                Func<CancellationToken, ValueTask<ProjectCompilerDiagnostics?>> factory,
                Func<ProjectCompilerDiagnostics, long> _,
                Func<ProjectCompilerDiagnostics, bool> _,
                CancellationToken cancellationToken) => factory(cancellationToken));

        _resolverFactory
            .Setup(item => item.Create(
                It.IsAny<Solution>(),
                It.IsAny<WorkspaceIdentity>(),
                It.IsAny<WorkspaceProjectTargetFrameworkMap>(),
                It.IsAny<SnapshotPrecondition>()))
            .Returns(_resolver.Object);

        _pathServiceFactory
            .Setup(item => item.Create(It.IsAny<WorkspaceIdentity>()))
            .Returns(_pathService.Object);

        _pathService
            .Setup(item => item.TryNormalizePath(It.IsAny<string>(), out It.Ref<string?>.IsAny))
            .Returns((string path, out string? normalizedPath) =>
            {
                normalizedPath = path.Replace("/workspace/", string.Empty, StringComparison.Ordinal);
                return true;
            });

        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: true));

        var options = Options.Create(new WorkspaceOptions { DefaultMaxResults = 100 });
        _target = new TransactionCompilerValidationService(
            _changeDetector.Object,
            _cacheScopeFactory.Object,
            _resolverFactory.Object,
            _pathServiceFactory.Object,
            _pathComparison.Object,
            options);
    }

    [Fact]
    public async Task GIVEN_NoActiveTransaction_WHEN_Validating_THEN_ShouldRejectInvalidState()
    {
        using var solution = CreateSolution("class C { }");
        var session = CreateSession(solution.Solution, solution.Solution) with
        {
            Transaction = null,
        };

        var action = async () => await _target.ValidateAsync(
            session,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GIVEN_ExistingErrorMovesWithinDocument_WHEN_Validating_THEN_ShouldNotReportNewError()
    {
        using var solution = CreateSolution("class C { Missing value; }");
        var staged = ChangeDocument(solution, "\nclass C { Missing value; }");
        var session = CreateSession(solution.Solution, staged);

        var result = await _target.ValidateAsync(session, TestContext.Current.CancellationToken);

        result.IsComplete.Should().BeTrue();
        result.Succeeded.Should().BeTrue();
        result.BaselineErrorCount.Should().Be(1);
        result.StagedErrorCount.Should().Be(1);
        result.IntroducedErrorCount.Should().Be(0);
        result.IntroducedDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_AdditionalDuplicateError_WHEN_Validating_THEN_ShouldUseMultisetDifference()
    {
        using var solution = CreateSolution("class C { Missing first; }");
        var staged = ChangeDocument(
            solution,
            "class C { Missing first; Missing second; }");

        var session = CreateSession(solution.Solution, staged);

        var result = await _target.ValidateAsync(session, TestContext.Current.CancellationToken);

        result.IsComplete.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
        result.BaselineErrorCount.Should().Be(1);
        result.StagedErrorCount.Should().Be(2);
        result.IntroducedErrorCount.Should().Be(1);
        result.IntroducedDiagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CS0246");
    }

    [Fact]
    public async Task GIVEN_ChangedDependencyBreaksDependant_WHEN_Validating_THEN_ShouldEvaluateTransitiveDependant()
    {
        var projects = new InMemoryRoslynProjectDefinition[]
        {
            new()
            {
                Name = "Dependency",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Dependency.cs",
                        Source = "public static class Dependency { public static int Value => 1; }",
                    },
                ],
            },
            new()
            {
                Name = "Dependant",
                ProjectReferences = ["Dependency"],
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Dependant.cs",
                        Source = "public class Dependant { private int value = Dependency.Value; }",
                    },
                ],
            },
        };

        using var solution = RoslynTestFactory.CreateSolution(projects);
        var dependencyDocument = solution.GetDocument("Dependency.cs", "Dependency");
        var staged = dependencyDocument.WithText(SourceText.From(
            "public static class Dependency { public static string Value => string.Empty; }")).Project.Solution;

        var session = CreateSession(solution.Solution, staged);

        var result = await _target.ValidateAsync(session, TestContext.Current.CancellationToken);

        result.Projects.Should().HaveCount(2);
        result.Projects.Should().Contain(item => item.Project.EndsWith("Dependant.csproj", StringComparison.Ordinal));
        result.IntroducedDiagnostics.Should().Contain(item => item.Id == "CS0029");
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_ExternalInputsChanged_WHEN_Validating_THEN_ShouldFailClosedWithoutCompiling()
    {
        using var solution = CreateSolution("class C { }");
        var staged = ChangeDocument(solution, "class C { int Value => 1; }");
        var session = CreateSession(solution.Solution, staged);
        _changeDetector
            .Setup(item => item.HasChanged(session.InputManifest, It.IsAny<CancellationToken>()))
            .Returns(true);

        var result = await _target.ValidateAsync(session, TestContext.Current.CancellationToken);

        result.IsComplete.Should().BeFalse();
        result.Succeeded.Should().BeFalse();
        result.IncompleteReasons.Should().Equal(
            TransactionCompilerValidationIncompleteReason.ExternalWorkspaceInputsChanged);
        result.Limitations.Should().ContainSingle(item => item.StartsWith("External Workspace inputs changed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_LoadError_WHEN_Validating_THEN_ShouldReportIncompleteAssurance()
    {
        using var solution = CreateSolution("class C { }");
        var staged = ChangeDocument(solution, "class C { int Value => 1; }");
        var loadDiagnostic = new DiagnosticInfo
        {
            Id = "LoadError",
            Severity = Results.DiagnosticSeverity.Error,
            Message = "Message",
        };

        var session = CreateSession(solution.Solution, staged) with
        {
            LoadDiagnostics = [loadDiagnostic],
        };

        var result = await _target.ValidateAsync(session, TestContext.Current.CancellationToken);

        result.IsComplete.Should().BeFalse();
        result.IncompleteReasons.Should().Equal(
            TransactionCompilerValidationIncompleteReason.WorkspaceLoadError);
        result.Limitations.Should().Contain("Workspace load diagnostic LoadError: Message");
    }

    [Fact]
    public async Task GIVEN_CompilationIsUnavailable_WHEN_Validating_THEN_ShouldReportProjectLimitation()
    {
        using var solution = CreateSolution("class C { }");
        var staged = ChangeDocument(solution, "class C { int Value => 1; }");
        var session = CreateSession(solution.Solution, staged);
        var incomplete = ProjectCompilerDiagnostics.Incomplete("Failure");

        _cacheScope
            .Setup(item => item.GetOrCreateAsync<ProjectCompilerDiagnosticsCacheKey, ProjectCompilerDiagnostics>(
                It.IsAny<ProjectCompilerDiagnosticsCacheKey>(),
                It.IsAny<Func<CancellationToken, ValueTask<ProjectCompilerDiagnostics?>>>(),
                It.IsAny<Func<ProjectCompilerDiagnostics, long>>(),
                It.IsAny<Func<ProjectCompilerDiagnostics, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(incomplete);

        var result = await _target.ValidateAsync(session, TestContext.Current.CancellationToken);

        result.IsComplete.Should().BeFalse();
        result.IncompleteReasons.Should().Equal(
            TransactionCompilerValidationIncompleteReason.CompilationUnavailable);
        result.Projects.Should().ContainSingle()
            .Which.IsComplete.Should().BeFalse();
        result.Limitations.Should().Contain(item => item.Contains("Failure", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_OneSnapshotCompilationIsUnavailable_WHEN_Validating_THEN_ShouldIdentifyFailedSnapshot(
        bool baselineIsComplete)
    {
        using var solution = CreateSolution("class C { }");
        var staged = ChangeDocument(solution, "class C { int Value => 1; }");
        var session = CreateSession(solution.Solution, staged);
        var complete = ProjectCompilerDiagnostics.Complete([]);
        var incomplete = ProjectCompilerDiagnostics.Incomplete("Failure");

        var firstResult = baselineIsComplete ? complete : incomplete;
        var secondResult = baselineIsComplete ? incomplete : complete;
        _cacheScope
            .SetupSequence(item => item.GetOrCreateAsync<ProjectCompilerDiagnosticsCacheKey, ProjectCompilerDiagnostics>(
                It.IsAny<ProjectCompilerDiagnosticsCacheKey>(),
                It.IsAny<Func<CancellationToken, ValueTask<ProjectCompilerDiagnostics?>>>(),
                It.IsAny<Func<ProjectCompilerDiagnostics, long>>(),
                It.IsAny<Func<ProjectCompilerDiagnostics, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstResult)
            .ReturnsAsync(secondResult);

        var result = await _target.ValidateAsync(session, TestContext.Current.CancellationToken);

        result.IsComplete.Should().BeFalse();
        result.Limitations.Should().ContainSingle(item => item.Contains("Failure", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_CacheProducesNoResult_WHEN_Validating_THEN_ShouldFailClosed()
    {
        using var solution = CreateSolution("class C { }");
        var staged = ChangeDocument(solution, "class C { int Value => 1; }");
        var session = CreateSession(solution.Solution, staged);

        _cacheScope
            .Setup(item => item.GetOrCreateAsync<ProjectCompilerDiagnosticsCacheKey, ProjectCompilerDiagnostics>(
                It.IsAny<ProjectCompilerDiagnosticsCacheKey>(),
                It.IsAny<Func<CancellationToken, ValueTask<ProjectCompilerDiagnostics?>>>(),
                It.IsAny<Func<ProjectCompilerDiagnostics, long>>(),
                It.IsAny<Func<ProjectCompilerDiagnostics, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectCompilerDiagnostics?)null);

        var result = await _target.ValidateAsync(session, TestContext.Current.CancellationToken);

        result.IsComplete.Should().BeFalse();
        result.Limitations.Should().Contain(
            item => item.Contains("caching did not produce a result", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_ProjectPathCannotBeNormalised_WHEN_Validating_THEN_ShouldUseProjectName()
    {
        using var solution = CreateSolution("class C { }");
        var staged = ChangeDocument(solution, "class C { int Value => 1; }");
        var session = CreateSession(solution.Solution, staged);
        _pathService
            .Setup(item => item.TryNormalizePath(It.IsAny<string>(), out It.Ref<string?>.IsAny))
            .Returns(false);

        var result = await _target.ValidateAsync(session, TestContext.Current.CancellationToken);

        result.Projects.Should().ContainSingle()
            .Which.Project.Should().Be("Project");
    }

    [Fact]
    public async Task GIVEN_NonSourceCompilerDiagnostics_WHEN_Validating_THEN_ShouldUseStableLocationKind()
    {
        using var solution = CreateSolution("class C { }");
        var project = solution.Solution.Projects.Single();
        var baseline = project.Solution.WithProjectCompilationOptions(
            project.Id,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        var document = baseline.GetDocument(project.Documents.Single().Id)
            ?? throw new InvalidOperationException("The test document must remain in the project.");

        var staged = document.WithText(SourceText.From("class C { int Value => 1; }")).Project.Solution;
        var session = CreateSession(baseline, staged);

        var result = await _target.ValidateAsync(session, TestContext.Current.CancellationToken);

        result.IsComplete.Should().BeTrue();
        result.BaselineErrorCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_AffectedProjectIsAbsentFromStagedSnapshot_WHEN_Validating_THEN_ShouldFailClosed()
    {
        using var solution = CreateSolution("class C { }");
        var project = solution.Solution.Projects.Single();
        var staged = solution.Solution.RemoveProject(project.Id);
        var session = CreateSession(solution.Solution, staged);

        var result = await _target.ValidateAsync(session, TestContext.Current.CancellationToken);

        result.IsComplete.Should().BeFalse();
        result.IncompleteReasons.Should().Equal(
            TransactionCompilerValidationIncompleteReason.ProjectUnavailable);
        result.Projects.Should().ContainSingle()
            .Which.Project.Should().EndWith("Project.csproj");

        result.Limitations.Should().ContainSingle(
            item => item.Contains("not available in both transaction snapshots", StringComparison.Ordinal));
    }

    private static InMemoryRoslynSolution CreateSolution(string source)
    {
        var project = new InMemoryRoslynProjectDefinition
        {
            Name = "Project",
            Documents =
            [
                new InMemoryRoslynDocumentDefinition
                {
                    Name = "Code.cs",
                    Source = source,
                },
            ],
        };

        return RoslynTestFactory.CreateSolution([project]);
    }

    private static Solution ChangeDocument(
        InMemoryRoslynSolution solution,
        string source)
    {
        var document = solution.GetDocument("Code.cs");
        var text = SourceText.From(source);
        var staged = document.WithText(text).Project.Solution;
        return staged;
    }

    private static WorkspaceSessionSnapshot CreateSession(Solution baseline, Solution staged)
    {
        var baselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1);
        var revision = new WorkspaceTransactionRevision
        {
            SnapshotId = WorkspaceSnapshotTestFactory.CreateId(2),
            Solution = staged,
            Changes = new ChangeSummary(),
            Operation = "Operation",
            Summary = "Summary",
            Preview = new MutationPreview { Summary = "Summary" },
        };

        var transaction = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = baselineSnapshotId,
            BaselineSolution = baseline,
            Revisions = [revision],
            CurrentRevision = 1,
            MaxRevisions = 5,
        };

        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 1,
            LoadedPath = "/workspace/Project.csproj",
            WorkspaceRoot = "/workspace",
        };

        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var operationGate = new Mock<IWorkspaceOperationGate>();
        var snapshotIdentity = WorkspaceSnapshotIdentity.Create(
            workspaceIdentity,
            baselineSnapshotId,
            transaction);

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = baselineSnapshotId,
            State = WorkspaceLifecycleState.TransactionActive,
            Workspace = workspaceIdentity,
            LoadedWorkspace = loadedWorkspace.Object,
            CurrentSolution = staged,
            Transaction = transaction,
            InputManifest = new WorkspaceInputManifest(),
            OperationGate = operationGate.Object,
            CurrentSnapshotIdentity = snapshotIdentity,
        };
    }
}
