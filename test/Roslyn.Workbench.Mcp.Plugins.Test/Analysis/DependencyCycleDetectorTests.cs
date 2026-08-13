namespace Roslyn.Workbench.Mcp.Plugins.Test.Analysis;

public sealed class DependencyCycleDetectorTests
{
    [Fact]
    public void GIVEN_DeepAcyclicGraph_WHEN_FindingCycles_THEN_ShouldCompleteWithoutRecursiveTraversal()
    {
        const int nodeCount = 20_000;
        var nodes = CreateNodes(nodeCount);
        var edges = CreateChainEdges(nodeCount, includeCycle: false);

        var result = DependencyCycleDetector.FindCycles(nodes, edges, TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_DeepCyclicGraph_WHEN_FindingCycles_THEN_ShouldReturnCompleteComponent()
    {
        const int nodeCount = 20_000;
        var nodes = CreateNodes(nodeCount);
        var edges = CreateChainEdges(nodeCount, includeCycle: true);

        var result = DependencyCycleDetector.FindCycles(nodes, edges, TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].Nodes.Should().HaveCount(nodeCount);
        result[0].Nodes.Select(static node => node.DisplayName).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void GIVEN_MultipleCyclesAndUnknownEdges_WHEN_FindingCycles_THEN_ShouldReturnDeterministicCycles()
    {
        var nodes = CreateNodes(4);
        var edges = new[]
        {
            CreateEdge(0, 0),
            CreateEdge(1, 2),
            CreateEdge(2, 1),
            new GraphEdge
            {
                FromId = "Unknown",
                FromDisplayName = "Unknown",
                ToId = "Node00003",
                ToDisplayName = "Node00003",
                Kind = "Dependency",
            },
            new GraphEdge
            {
                FromId = "Node00003",
                FromDisplayName = "Node00003",
                ToId = "Unknown",
                ToDisplayName = "Unknown",
                Kind = "Dependency",
            },
        };

        var result = DependencyCycleDetector.FindCycles(nodes.Reverse().ToArray(), edges, TestContext.Current.CancellationToken);

        result.Should().HaveCount(2);
        result[0].Nodes.Select(static node => node.Id).Should().Equal("Node00000");
        result[1].Nodes.Select(static node => node.Id).Should().Equal("Node00001", "Node00002");
    }

    [Fact]
    public void GIVEN_CancellationRequested_WHEN_FindingCycles_THEN_ShouldStopTraversal()
    {
        var nodes = CreateNodes(2);
        var edges = CreateChainEdges(2, includeCycle: false);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => DependencyCycleDetector.FindCycles(nodes, edges, cancellation.Token);

        action.Should().Throw<OperationCanceledException>();
    }

    private static GraphNode[] CreateNodes(int count)
    {
        return Enumerable.Range(0, count)
            .Select(static index => new GraphNode
            {
                Id = $"Node{index:D5}",
                Kind = "Type",
                DisplayName = $"Node{index:D5}",
            })
            .ToArray();
    }

    private static GraphEdge[] CreateChainEdges(int nodeCount, bool includeCycle)
    {
        var edgeCount = includeCycle ? nodeCount : nodeCount - 1;
        var edges = new GraphEdge[edgeCount];
        for (var index = 0; index < nodeCount - 1; index++)
        {
            edges[index] = CreateEdge(index, index + 1);
        }

        if (includeCycle)
        {
            edges[^1] = CreateEdge(nodeCount - 1, 0);
        }

        return edges;
    }

    private static GraphEdge CreateEdge(int fromIndex, int toIndex)
    {
        return new GraphEdge
        {
            FromId = $"Node{fromIndex:D5}",
            FromDisplayName = $"Node{fromIndex:D5}",
            ToId = $"Node{toIndex:D5}",
            ToDisplayName = $"Node{toIndex:D5}",
            Kind = "Dependency",
        };
    }
}
