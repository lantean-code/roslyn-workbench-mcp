namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetDependencyGraphToolTests
{
    [Fact]
    public async Task GIVEN_GranularityIsUnsupported_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new GetDependencyGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        dependencyAnalysisService
            .Setup(item => item.IsSupportedGraphGranularity("Granularity"))
            .Returns(false);

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Granularity",
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "Granularity must be Project, Namespace, Type, or Symbol.",
        });
    }

    [Fact]
    public async Task GIVEN_MaxDepthIsNegative_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new GetDependencyGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        dependencyAnalysisService
            .Setup(item => item.IsSupportedGraphGranularity("Type"))
            .Returns(true);

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Type",
            MaxDepth = -1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "MaxDepth must be zero or greater.",
        });
    }

    [Fact]
    public async Task GIVEN_NodesLimitIsNegative_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new GetDependencyGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        dependencyAnalysisService
            .Setup(item => item.IsSupportedGraphGranularity("Type"))
            .Returns(true);

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Type",
            NodesLimit = new CollectionLimit
            {
                MaxResults = -1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "NodesLimit and EdgesLimit must be zero or greater when provided.",
        });
    }

    [Fact]
    public async Task GIVEN_EdgesLimitIsNegative_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new GetDependencyGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        dependencyAnalysisService
            .Setup(item => item.IsSupportedGraphGranularity("Type"))
            .Returns(true);

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Type",
            EdgesLimit = new CollectionLimit
            {
                MaxResults = -1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "NodesLimit and EdgesLimit must be zero or greater when provided.",
        });
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetDependencyGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var expected = PluginExecutionResult<DependencyGraphData>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        dependencyAnalysisService
            .Setup(item => item.IsSupportedGraphGranularity("Type"))
            .Returns(true);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DependencyGraphData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyGraphData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Type",
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveProjectsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("class Formatter {}");

        var target = new GetDependencyGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var expected = PluginExecutionResult<DependencyGraphData>.Rejected(new PluginExecutionError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        dependencyAnalysisService
            .Setup(item => item.IsSupportedGraphGranularity("Type"))
            .Returns(true);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DependencyGraphData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyGraphData>
            {
                Value = [document.Document],
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DependencyGraphData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, DependencyGraphData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Type",
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_GraphContainsEdgesOutsideBoundedNodes_WHEN_CallingExecuteAsync_THEN_ShouldReturnOnlyEdgesForIncludedNodes()
    {
        using var document = RoslynTestFactory.CreateDocument("class Formatter {}");

        var target = new GetDependencyGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var project = document.Solution.Projects.Single();
        var nodes = new[]
        {
            new GraphNode
            {
                Id = "A",
                Kind = "Type",
                DisplayName = "A",
            },
            new GraphNode
            {
                Id = "B",
                Kind = "Type",
                DisplayName = "B",
            },
            new GraphNode
            {
                Id = "C",
                Kind = "Type",
                DisplayName = "C",
            },
        };
        var edges = new[]
        {
            new GraphEdge
            {
                FromId = "A",
                FromDisplayName = "A",
                ToId = "B",
                ToDisplayName = "B",
                Kind = "Contains",
            },
            new GraphEdge
            {
                FromId = "B",
                FromDisplayName = "B",
                ToId = "C",
                ToDisplayName = "C",
                Kind = "Contains",
            },
        };

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        dependencyAnalysisService
            .Setup(item => item.IsSupportedGraphGranularity("Type"))
            .Returns(true);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DependencyGraphData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyGraphData>
            {
                Value = [document.Document],
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DependencyGraphData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, DependencyGraphData>
            {
                Value = [project],
            });
        dependencyAnalysisService
            .Setup(item => item.BuildGraphAsync(
                "Type",
                It.Is<IReadOnlyList<Project>>(projects => projects.Count == 1 && projects[0] == project),
                It.Is<IReadOnlyList<Document>>(documents => documents.Count == 1 && documents[0] == document.Document),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((nodes, edges));

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Type",
            NodesLimit = new CollectionLimit
            {
                MaxResults = 2,
            },
            EdgesLimit = new CollectionLimit
            {
                MaxResults = 10,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Nodes.Items.Select(item => item.Id).Should().Equal("A", "B");
        result.Data.Nodes.HasMore.Should().BeTrue();
        result.Data.Edges.Items.Should().ContainSingle();
        result.Data.Edges.Items[0].FromId.Should().Be("A");
        result.Data.Edges.Items[0].ToId.Should().Be("B");
    }
}
