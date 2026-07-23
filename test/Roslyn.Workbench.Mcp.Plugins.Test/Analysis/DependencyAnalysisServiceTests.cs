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
        var (cycles, totalCount) = await target.FindCyclesAsync(
            "Type",
            [project],
            [document.Document],
            0,
            queryContext.Object,
            TestContext.Current.CancellationToken);

        cycles.Should().BeEmpty();
        totalCount.Should().Be(1);
    }
}
