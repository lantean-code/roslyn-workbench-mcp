namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindDependencyCyclesToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        FindDependencyCyclesTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<FindDependencyCyclesRequest, DependencyCyclesData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "find-dependency-cycles"
                && metadata.Title == "Find Dependency Cycles"
                && metadata.Description == "Returns detected dependency cycles for the selected scope and granularity."),
            It.IsAny<IQueryToolHandler<FindDependencyCyclesRequest, DependencyCyclesData>>()), Times.Once);
    }

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

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
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
        var expected = PluginExecutionResult<DependencyCyclesData>.Rejected(new ToolError
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
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyCyclesData>
            {
                Rejection = expected,
            });

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
        var expected = PluginExecutionResult<DependencyCyclesData>.Rejected(new ToolError
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
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyCyclesData>
            {
                Value = [document.Document],
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, DependencyCyclesData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindDependencyCyclesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        dependencyAnalysisService.Verify(item => item.FindCyclesAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<Project>>(),
            It.IsAny<IReadOnlyList<Document>>(),
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
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyCyclesData>
            {
                Value = [document.Document],
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, DependencyCyclesData>
            {
                Value = [document.Document.Project!],
            });
        dependencyAnalysisService
            .Setup(item => item.FindCyclesAsync(
                "Type",
                It.Is<IReadOnlyList<Project>>(projects => projects.Count == 1 && projects[0] == document.Document.Project),
                It.Is<IReadOnlyList<Document>>(documents => documents.Count == 1 && documents[0] == document.Document),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cycles);

        var result = await target.ExecuteAsync(new FindDependencyCyclesRequest
        {
            Granularity = "Type",
            CyclesLimit = new CollectionLimit
            {
                MaxResults = 1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Cycles.Items.Should().ContainSingle();
        result.Data.Cycles.Items[0].Should().BeEquivalentTo(cycles[0]);
        result.Data.Cycles.HasMore.Should().BeTrue();
    }
}
