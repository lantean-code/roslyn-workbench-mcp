using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Test.Analysis;

public sealed class DependencyAnalysisServiceTests
{
    [Theory]
    [InlineData("Project", true)]
    [InlineData("Namespace", true)]
    [InlineData("Type", true)]
    [InlineData("Symbol", false)]
    public void GIVEN_GranularityValue_WHEN_CheckingCycleSupport_THEN_ShouldReturnExpectedResult(string value, bool expected)
    {
        var target = new DependencyAnalysisService();

        var result = target.IsSupportedCycleGranularity(value);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Project", true)]
    [InlineData("Namespace", true)]
    [InlineData("Type", true)]
    [InlineData("Symbol", true)]
    [InlineData("Invalid", false)]
    public void GIVEN_GranularityValue_WHEN_CheckingGraphSupport_THEN_ShouldReturnExpectedResult(string value, bool expected)
    {
        var target = new DependencyAnalysisService();

        var result = target.IsSupportedGraphGranularity(value);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task GIVEN_ProjectGraphExceedsLimits_WHEN_BuildingGraph_THEN_ShouldReturnBoundedNodesAndEdges()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Alpha",
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Alpha.cs", Source = "class Alpha { }" }],
                ProjectReferences = ["Beta"],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Beta",
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Beta.cs", Source = "class Beta { }" }],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Gamma",
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Gamma.cs", Source = "class Gamma { }" }],
            },
        ]);

        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var (nodes, nodesHaveMore, edges, edgesHaveMore) = await target.BuildGraphAsync(
            "Project",
            solution.Solution.Projects.ToArray(),
            solution.Solution.Projects.SelectMany(static project => project.Documents).ToArray(),
            2,
            0,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        nodes.Select(static node => node.DisplayName).Should().Equal("Alpha", "Beta");
        nodesHaveMore.Should().BeTrue();
        edges.Should().BeEmpty();
        edgesHaveMore.Should().BeTrue();
    }

    [Theory]
    [InlineData("Namespace", "Sample", "Sample")]
    [InlineData("Type", "Alpha", "Beta")]
    [InlineData("Symbol", "Create", "Beta")]
    public async Task GIVEN_SourceGraphContainsDependencies_WHEN_BuildingGraph_THEN_ShouldReturnOrderedEdges(
        string granularity,
        string expectedFromName,
        string expectedToName)
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public sealed class Alpha
            {
                public Beta Create()
                {
                    return new Beta();
                }
            }

            public sealed class Beta
            {
            }
            """);

        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContext
            .SetupGet(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);

        workspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var project = document.Solution.Projects.Single();
        var (nodes, nodesHaveMore, edges, edgesHaveMore) = await target.BuildGraphAsync(
            granularity,
            [project],
            [document.Document],
            100,
            100,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        nodes.Should().NotBeEmpty();
        nodesHaveMore.Should().BeFalse();
        edges.Should().Contain(edge => edge.FromDisplayName.Contains(expectedFromName, StringComparison.Ordinal)
            && edge.ToDisplayName.Contains(expectedToName, StringComparison.Ordinal));

        edgesHaveMore.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_MultipleImpactedTestsExceedLimit_WHEN_FindingTestImpacts_THEN_ShouldStopAfterDetectingAdditionalResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public sealed class Target
            {
                public void Execute()
                {
                }
            }

            public sealed class TargetTests
            {
                public void AlphaTest()
                {
                    new Target().Execute();
                }

                public void BetaTest()
                {
                    new Target().Execute();
                }

                public void GammaTest()
                {
                }
            }
            """);

        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var targetSymbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Target",
            TestContext.Current.CancellationToken);

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContext
            .SetupGet(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);

        workspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        workspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));

        var (tests, hasMore) = await target.FindTestImpactsAsync(
            targetSymbol,
            [document.Document],
            true,
            1,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        tests.Should().ContainSingle();
        tests[0].Test!.DisplayName.Should().Contain("AlphaTest");
        tests[0].Reasons.Should().ContainSingle();
        hasMore.Should().BeTrue();
        workspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_TypeDependencyCycleExceedsZeroLimit_WHEN_FindingCycles_THEN_ShouldReportAdditionalResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public sealed class Alpha
            {
                public Beta Value { get; } = new();
            }

            public sealed class Beta
            {
                public Alpha Value { get; } = new();
            }
            """);

        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContext
            .SetupGet(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);

        workspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var project = document.Solution.Projects.Single();
        var result = await target.FindCyclesAsync(
            "Type",
            [project],
            [document.Document],
            0,
            100,
            100,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(DependencyCycleAnalysisStatus.Completed);
        result.Cycles.Should().BeEmpty();
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_ProjectReferenceGraph_WHEN_FindingCycles_THEN_ShouldUseSolutionDependencyGraph()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Alpha",
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Alpha.cs", Source = "class Alpha { }" }],
                ProjectReferences = ["Beta"],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Beta",
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Beta.cs", Source = "class Beta { }" }],
            },
        ]);

        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var result = await target.FindCyclesAsync(
            "Project",
            solution.Solution.Projects.ToArray(),
            solution.Solution.Projects.SelectMany(static project => project.Documents).ToArray(),
            25,
            100,
            100,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(DependencyCycleAnalysisStatus.Completed);
        result.Cycles.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GIVEN_MultiTargetProjectNodesShareProjectPath_WHEN_FindingCycles_THEN_ShouldKeepFrameworkNodesDistinct()
    {
        const string sharedProjectPath = "/workspace/Alpha/Alpha.csproj";
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Alpha(net8.0)",
                FilePath = sharedProjectPath,
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Alpha8.cs", Source = "class Alpha8 { }" }],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Alpha(net9.0)",
                FilePath = sharedProjectPath,
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Alpha9.cs", Source = "class Alpha9 { }" }],
            },
        ]);

        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var projects = solution.Solution.Projects.ToArray();
        var documents = projects.SelectMany(static project => project.Documents).ToArray();
        var graph = await target.BuildGraphAsync(
            "Project",
            projects,
            documents,
            100,
            100,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        var cycles = await target.FindCyclesAsync(
            "Project",
            projects,
            documents,
            25,
            100,
            100,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        graph.Nodes.Should().HaveCount(2);
        graph.Nodes.Select(static node => node.Id).Should().OnlyHaveUniqueItems();
        cycles.Status.Should().Be(DependencyCycleAnalysisStatus.Completed);
        cycles.Cycles.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_MultiTargetProjectsContainSameTypes_WHEN_FindingCycles_THEN_ShouldKeepFrameworkTypeNodesDistinct()
    {
        const string sharedProjectPath = "/workspace/Alpha/Alpha.csproj";
        const string source = "namespace Sample; class Alpha { public Beta Value { get; } = new(); } class Beta { public Alpha Value { get; } = new(); }";
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Alpha(net8.0)",
                FilePath = sharedProjectPath,
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Types.cs", Source = source }],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Alpha(net9.0)",
                FilePath = sharedProjectPath,
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Types.cs", Source = source }],
            },
        ]);

        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        queryContext
            .SetupGet(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);

        workspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var projects = solution.Solution.Projects.ToArray();
        var documents = projects.SelectMany(static project => project.Documents).ToArray();
        var graph = await target.BuildGraphAsync(
            "Type",
            projects,
            documents,
            100,
            100,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        var cycles = await target.FindCyclesAsync(
            "Type",
            projects,
            documents,
            25,
            100,
            100,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        graph.Nodes.Should().HaveCount(4);
        graph.Nodes.Select(static node => node.Id).Should().OnlyHaveUniqueItems();
        graph.Edges.Should().HaveCount(4);
        cycles.Status.Should().Be(DependencyCycleAnalysisStatus.Completed);
        cycles.Cycles.Should().HaveCount(2);
    }

    [Fact]
    public async Task GIVEN_DifferentProjectsContainSameFullyQualifiedType_WHEN_BuildingGraph_THEN_ShouldKeepProjectTypesDistinct()
    {
        const string source = "namespace Sample; class Shared { }";
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Alpha",
                FilePath = "/workspace/Alpha/Alpha.csproj",
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Shared.cs", Source = source }],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Beta",
                FilePath = "/workspace/Beta/Beta.csproj",
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Shared.cs", Source = source }],
            },
        ]);

        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        queryContext
            .SetupGet(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);

        workspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var projects = solution.Solution.Projects.ToArray();
        var graph = await target.BuildGraphAsync(
            "Type",
            projects,
            projects.SelectMany(static project => project.Documents).ToArray(),
            100,
            100,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        graph.Nodes.Should().HaveCount(2);
        graph.Nodes.Select(static node => node.Id).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(1, 100, DependencyCycleAnalysisStatus.NodeLimitExceeded)]
    [InlineData(100, 0, DependencyCycleAnalysisStatus.EdgeLimitExceeded)]
    public async Task GIVEN_ProjectReferenceGraphExceedsAnalysisLimit_WHEN_FindingCycles_THEN_ShouldRejectPartialAnalysis(
        int maxNodes,
        int maxEdges,
        DependencyCycleAnalysisStatus expectedStatus)
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Alpha",
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Alpha.cs", Source = "class Alpha { }" }],
                ProjectReferences = ["Beta"],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Beta",
                Documents = [new InMemoryRoslynDocumentDefinition { Name = "Beta.cs", Source = "class Beta { }" }],
            },
        ]);

        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var result = await target.FindCyclesAsync(
            "Project",
            solution.Solution.Projects.ToArray(),
            solution.Solution.Projects.SelectMany(static project => project.Documents).ToArray(),
            25,
            maxNodes,
            maxEdges,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(expectedStatus);
        result.Cycles.Should().BeNull();
        result.TotalCount.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_TypeGraphExceedsNodeLimit_WHEN_FindingCycles_THEN_ShouldRejectPartialAnalysis()
    {
        using var document = RoslynTestFactory.CreateDocument("class Alpha { } class Beta { }");
        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        var result = await target.FindCyclesAsync(
            "Type",
            [document.Document.Project],
            [document.Document],
            25,
            1,
            100,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(DependencyCycleAnalysisStatus.NodeLimitExceeded);
        result.Cycles.Should().BeNull();
        result.TotalCount.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_TypeGraphExceedsEdgeLimit_WHEN_FindingCycles_THEN_ShouldRejectPartialAnalysis()
    {
        using var document = RoslynTestFactory.CreateDocument("class Alpha { public Beta Value { get; } = new(); } class Beta { public Alpha Value { get; } = new(); }");
        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContext
            .SetupGet(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);

        workspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.FindCyclesAsync(
            "Type",
            [document.Document.Project],
            [document.Document],
            25,
            100,
            1,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(DependencyCycleAnalysisStatus.EdgeLimitExceeded);
        result.Cycles.Should().BeNull();
        result.TotalCount.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_NamespaceGraphExceedsNodeLimit_WHEN_FindingCycles_THEN_ShouldStopDiscovery()
    {
        using var document = RoslynTestFactory.CreateDocument("namespace One { class Alpha { } } namespace Two { class Beta { } }");
        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        var result = await target.FindCyclesAsync(
            "Namespace",
            [document.Document.Project],
            [document.Document],
            25,
            1,
            100,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(DependencyCycleAnalysisStatus.NodeLimitExceeded);
    }

    [Fact]
    public async Task GIVEN_TargetAppearsInsideCompositeTypes_WHEN_AnalyzingDependencies_THEN_ShouldFindGraphEdgeAndTestImpact()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System.Collections.Generic;

            namespace Sample;

            public sealed class Customer
            {
            }

            public sealed class Consumer
            {
                public List<Customer> Values { get; } = [];
            }

            public sealed class CustomerTests
            {
                public Customer[] BuildTest()
                {
                    return [];
                }
            }
            """);

        var target = new DependencyAnalysisService();
        var queryContext = new Mock<IQueryContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var targetSymbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Customer",
            TestContext.Current.CancellationToken);

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContext
            .SetupGet(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);

        workspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        workspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var project = document.Solution.Projects.Single();
        var (_, _, edges, _) = await target.BuildGraphAsync(
            "Type",
            [project],
            [document.Document],
            100,
            100,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        var (tests, hasMore) = await target.FindTestImpactsAsync(
            targetSymbol,
            [document.Document],
            includeReasons: false,
            10,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        edges.Should().Contain(edge => edge.FromDisplayName == "Consumer" && edge.ToDisplayName == "Customer");
        tests.Should().ContainSingle(item => item.Test!.DisplayName.Contains("BuildTest", StringComparison.Ordinal));
        hasMore.Should().BeFalse();
    }
}
