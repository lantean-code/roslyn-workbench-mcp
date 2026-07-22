namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindDependencyCyclesToolTests
{
    [Fact]
    public async Task GIVEN_GranularityIsUnsupported_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new FindDependencyCyclesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        dependencyAnalysisService
            .Setup(item => item.IsSupportedCycleGranularity("Invalid"))
            .Returns(false);

        var result = await target.ExecuteAsync(new FindDependencyCyclesRequest
        {
            Granularity = "Invalid",
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "Granularity must be Project, Namespace, or Type.",
        });
        queryContextMocks.RequestResolver.Verify(item => item.ResolveDocuments<DependencyCyclesData>(It.IsAny<ScopeSelector?>(), It.IsAny<IToolExecutionContext>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindDependencyCyclesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var expected = PluginExecutionResult<DependencyCyclesData>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        dependencyAnalysisService
            .Setup(item => item.IsSupportedCycleGranularity("Type"))
            .Returns(true);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, DependencyCyclesData>.Rejected(expected));

        var result = await target.ExecuteAsync(new FindDependencyCyclesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        queryContextMocks.RequestResolver.Verify(item => item.ResolveProjects<DependencyCyclesData>(It.IsAny<ScopeSelector?>(), It.IsAny<IToolExecutionContext>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolveProjectsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("class Formatter { }");

        var target = new FindDependencyCyclesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var expected = PluginExecutionResult<DependencyCyclesData>.Rejected(new PluginExecutionError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        dependencyAnalysisService
            .Setup(item => item.IsSupportedCycleGranularity("Type"))
            .Returns(true);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, DependencyCyclesData>.Resolved([document.Document]));
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Project>, DependencyCyclesData>.Rejected(expected));

        var result = await target.ExecuteAsync(new FindDependencyCyclesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        dependencyAnalysisService.Verify(item => item.FindCyclesAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<Project>>(),
            It.IsAny<IReadOnlyList<Document>>(),
            It.IsAny<int>(),
            It.IsAny<IQueryContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ValidRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedCycles()
    {
        using var document = RoslynTestFactory.CreateDocument("class Formatter { }");

        var target = new FindDependencyCyclesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var cycles = new[]
        {
            new DependencyCycle
            {
                Nodes =
                [
                    new GraphNode
                    {
                        Id = "CycleB",
                        Kind = "Type",
                        DisplayName = "CycleB",
                    },
                ],
            },
            new DependencyCycle
            {
                Nodes =
                [
                    new GraphNode
                    {
                        Id = "CycleA",
                        Kind = "Type",
                        DisplayName = "CycleA",
                    },
                ],
            },
        };

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(2);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        dependencyAnalysisService
            .Setup(item => item.IsSupportedCycleGranularity("Type"))
            .Returns(true);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, DependencyCyclesData>.Resolved([document.Document]));
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Project>, DependencyCyclesData>.Resolved([document.Document.Project!]));
        dependencyAnalysisService
            .Setup(item => item.FindCyclesAsync(
                "Type",
                It.Is<IReadOnlyList<Project>>(projects => projects.Count == 1 && projects[0] == document.Document.Project),
                It.Is<IReadOnlyList<Document>>(documents => documents.Count == 1 && documents[0] == document.Document),
                1,
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((cycles[..1], true));

        var result = await target.ExecuteAsync(new FindDependencyCyclesRequest
        {
            Granularity = "Type",
            CyclesLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Cycles.Items.Should().ContainSingle();
        result.Data.Cycles.Items[0].Should().BeEquivalentTo(cycles[0]);
        result.Data.Cycles.HasMore.Should().BeTrue();
    }
}
