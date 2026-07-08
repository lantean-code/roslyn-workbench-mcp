namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindDependencyCyclesToolTests
{
    [Fact]
    public async Task GIVEN_UnsupportedGranularity_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = CreateContext(dependencyAnalysisService: dependencyAnalysisService.Object);
        var target = new FindDependencyCyclesTool();

        dependencyAnalysisService
            .Setup(service => service.IsSupportedCycleGranularity("Granularity"))
            .Returns(false);

        var result = await target.ExecuteAsync(new FindDependencyCyclesRequest
        {
            Granularity = "Granularity",
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<DependencyCyclesData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = CreateContext(requestResolver: requestResolver.Object, dependencyAnalysisService: dependencyAnalysisService.Object);
        var target = new FindDependencyCyclesTool();

        dependencyAnalysisService
            .Setup(service => service.IsSupportedCycleGranularity("Type"))
            .Returns(true);
        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyCyclesData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindDependencyCyclesRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveProjectsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var documents = workspace.Solution.Projects.Single().Documents.ToArray();
        var expected = PluginExecutionResult<DependencyCyclesData>.Rejected(new ToolError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = CreateContext(workspace, requestResolver.Object, dependencyAnalysisService.Object);
        var target = new FindDependencyCyclesTool();

        dependencyAnalysisService
            .Setup(service => service.IsSupportedCycleGranularity("Type"))
            .Returns(true);
        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyCyclesData>
            {
                Value = documents,
            });
        requestResolver
            .Setup(resolver => resolver.ResolveProjects<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, DependencyCyclesData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindDependencyCyclesRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_DetectedCyclesExceedLimit_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedCycles()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var project = workspace.Solution.Projects.Single();
        var document = project.Documents.Single();
        var requestResolver = new Mock<IToolRequestResolver>();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = CreateContext(workspace, requestResolver.Object, dependencyAnalysisService.Object, defaultMaxResults: 1);
        var target = new FindDependencyCyclesTool();
        var expectedCycles = new[]
        {
            new DependencyCycle
            {
                Nodes =
                [
                    new GraphNode
                    {
                        Id = "A",
                        Kind = "Type",
                        DisplayName = "A",
                    },
                ],
            },
            new DependencyCycle
            {
                Nodes =
                [
                    new GraphNode
                    {
                        Id = "B",
                        Kind = "Type",
                        DisplayName = "B",
                    },
                ],
            },
        };

        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyCyclesData>
            {
                Value = [document],
            });
        requestResolver
            .Setup(resolver => resolver.ResolveProjects<DependencyCyclesData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, DependencyCyclesData>
            {
                Value = [project],
            });
        dependencyAnalysisService
            .Setup(service => service.IsSupportedCycleGranularity("Type"))
            .Returns(true);
        dependencyAnalysisService
            .Setup(service => service.FindCyclesAsync(
                "Type",
                It.Is<IReadOnlyList<Project>>(projects => projects.Count == 1 && projects[0] == project),
                It.Is<IReadOnlyList<Document>>(documents => documents.Count == 1 && documents[0] == document),
                context,
                CancellationToken.None))
            .ReturnsAsync(expectedCycles);

        var result = await target.ExecuteAsync(new FindDependencyCyclesRequest
        {
            Granularity = "Type",
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Cycles.Items.Should().HaveCount(1);
        result.Data.Cycles.HasMore.Should().BeTrue();
    }

    private static IQueryContext CreateContext(
        MiniWorkspace? workspace = null,
        IToolRequestResolver? requestResolver = null,
        IDependencyAnalysisService? dependencyAnalysisService = null,
        int defaultMaxResults = 100)
    {
        var currentWorkspace = workspace ?? MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var services = new ToolExecutionServicesBuilder()
            .WithRequestResolver(requestResolver ?? Mock.Of<IToolRequestResolver>())
            .WithDependencyAnalysisService(dependencyAnalysisService ?? Mock.Of<IDependencyAnalysisService>())
            .Build();

        return new QueryContextBuilder()
            .WithCurrentSolution(currentWorkspace.Solution)
            .WithResolver(currentWorkspace.CreateResolver(currentWorkspace.CreateWorkspaceIdentity()))
            .WithWorkspaceIdentity(currentWorkspace.CreateWorkspaceIdentity())
            .WithDefaultMaxResults(defaultMaxResults)
            .WithToolExecutionServices(services)
            .Build();
    }
}

public sealed class GetDependencyGraphToolTests
{
    [Fact]
    public async Task GIVEN_UnsupportedGranularity_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = CreateContext(dependencyAnalysisService: dependencyAnalysisService.Object);
        var target = new GetDependencyGraphTool();

        dependencyAnalysisService
            .Setup(service => service.IsSupportedGraphGranularity("Granularity"))
            .Returns(false);

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Granularity",
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_NegativeMaxDepth_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = CreateContext(dependencyAnalysisService: dependencyAnalysisService.Object);
        var target = new GetDependencyGraphTool();

        dependencyAnalysisService
            .Setup(service => service.IsSupportedGraphGranularity("Type"))
            .Returns(true);

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Type",
            MaxDepth = -1,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_NegativeCollectionLimit_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = CreateContext(dependencyAnalysisService: dependencyAnalysisService.Object);
        var target = new GetDependencyGraphTool();

        dependencyAnalysisService
            .Setup(service => service.IsSupportedGraphGranularity("Type"))
            .Returns(true);

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Type",
            NodesLimit = new CollectionLimit
            {
                MaxResults = -1,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<DependencyGraphData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = CreateContext(requestResolver: requestResolver.Object, dependencyAnalysisService: dependencyAnalysisService.Object);
        var target = new GetDependencyGraphTool();

        dependencyAnalysisService
            .Setup(service => service.IsSupportedGraphGranularity("Type"))
            .Returns(true);
        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<DependencyGraphData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyGraphData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Type",
        }, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveProjectsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var documents = workspace.Solution.Projects.Single().Documents.ToArray();
        var expected = PluginExecutionResult<DependencyGraphData>.Rejected(new ToolError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = CreateContext(workspace, requestResolver.Object, dependencyAnalysisService.Object);
        var target = new GetDependencyGraphTool();

        dependencyAnalysisService
            .Setup(service => service.IsSupportedGraphGranularity("Type"))
            .Returns(true);
        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<DependencyGraphData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyGraphData>
            {
                Value = documents,
            });
        requestResolver
            .Setup(resolver => resolver.ResolveProjects<DependencyGraphData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, DependencyGraphData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Type",
        }, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_FilteredGraphExceedsNodeLimit_WHEN_CallingExecuteAsync_THEN_ShouldReturnOnlyEdgesForIncludedNodes()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var project = workspace.Solution.Projects.Single();
        var document = project.Documents.Single();
        var requestResolver = new Mock<IToolRequestResolver>();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = CreateContext(workspace, requestResolver.Object, dependencyAnalysisService.Object);
        var target = new GetDependencyGraphTool();
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

        dependencyAnalysisService
            .Setup(service => service.IsSupportedGraphGranularity("Type"))
            .Returns(true);
        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<DependencyGraphData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DependencyGraphData>
            {
                Value = [document],
            });
        requestResolver
            .Setup(resolver => resolver.ResolveProjects<DependencyGraphData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, DependencyGraphData>
            {
                Value = [project],
            });
        dependencyAnalysisService
            .Setup(service => service.BuildGraphAsync(
                "Type",
                It.Is<IReadOnlyList<Project>>(projects => projects.Count == 1 && projects[0] == project),
                It.Is<IReadOnlyList<Document>>(documents => documents.Count == 1 && documents[0] == document),
                context,
                CancellationToken.None))
            .ReturnsAsync((nodes, edges));

        var result = await target.ExecuteAsync(new GetDependencyGraphRequest
        {
            Granularity = "Type",
            NodesLimit = new CollectionLimit
            {
                MaxResults = 2,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Nodes.Items.Should().HaveCount(2);
        result.Data.Nodes.HasMore.Should().BeTrue();
        result.Data.Edges.Items.Should().ContainSingle();
        result.Data.Edges.Items[0].FromId.Should().Be("A");
        result.Data.Edges.Items[0].ToId.Should().Be("B");
    }

    private static IQueryContext CreateContext(
        MiniWorkspace? workspace = null,
        IToolRequestResolver? requestResolver = null,
        IDependencyAnalysisService? dependencyAnalysisService = null)
    {
        var currentWorkspace = workspace ?? MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var workspaceIdentity = currentWorkspace.CreateWorkspaceIdentity();
        var services = new ToolExecutionServicesBuilder()
            .WithRequestResolver(requestResolver ?? Mock.Of<IToolRequestResolver>())
            .WithDependencyAnalysisService(dependencyAnalysisService ?? Mock.Of<IDependencyAnalysisService>())
            .Build();

        return new QueryContextBuilder()
            .WithCurrentSolution(currentWorkspace.Solution)
            .WithResolver(currentWorkspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(services)
            .Build();
    }
}
