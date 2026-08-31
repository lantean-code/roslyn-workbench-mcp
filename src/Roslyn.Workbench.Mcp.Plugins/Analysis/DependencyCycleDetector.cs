using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analysis;

/// <summary>
/// Finds strongly connected components in a dependency graph and returns the components that form cycles.
/// </summary>
internal static class DependencyCycleDetector
{
    /// <summary>
    /// Finds deterministic dependency cycles, including self-referencing nodes, in a bounded graph.
    /// </summary>
    /// <param name="nodes">The graph nodes keyed by their identifiers.</param>
    /// <param name="edges">The directed dependency edges between nodes.</param>
    /// <param name="cancellationToken">A token that cancels graph traversal.</param>
    /// <returns>The cycles ordered by size and stable node identity.</returns>
    public static IReadOnlyList<DependencyCycle> FindCycles(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges, CancellationToken cancellationToken)
    {
        var nodeLookup = nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
        var adjacency = CreateAdjacency(nodes, edges, nodeLookup, cancellationToken);
        var indexByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLinkByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        var componentStack = new Stack<string>();
        var onComponentStack = new HashSet<string>(StringComparer.Ordinal);
        var traversalStack = new Stack<TraversalFrame>();
        var cycles = new List<DependencyCycle>();
        var index = 0;

        var orderedNodes = nodes
            .OrderBy(static node => node.DisplayName, StringComparer.Ordinal)
            .ThenBy(static node => node.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (var node in orderedNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (indexByNodeId.ContainsKey(node.Id))
            {
                continue;
            }

            StartTraversal(node.Id);
            while (traversalStack.TryPeek(out var frame))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (frame.TryGetNext(out var nextNodeId))
                {
                    if (!indexByNodeId.TryGetValue(nextNodeId, out var nextIndex))
                    {
                        StartTraversal(nextNodeId);
                    }
                    else if (onComponentStack.Contains(nextNodeId))
                    {
                        lowLinkByNodeId[frame.NodeId] = Math.Min(lowLinkByNodeId[frame.NodeId], nextIndex);
                    }

                    continue;
                }

                traversalStack.Pop();
                if (traversalStack.TryPeek(out var parentFrame))
                {
                    lowLinkByNodeId[parentFrame.NodeId] = Math.Min(lowLinkByNodeId[parentFrame.NodeId], lowLinkByNodeId[frame.NodeId]);
                }

                if (lowLinkByNodeId[frame.NodeId] == indexByNodeId[frame.NodeId])
                {
                    AddComponent(frame.NodeId);
                }
            }
        }

        return cycles
            .OrderBy(static cycle => cycle.Nodes.Count)
            .ThenBy(static cycle => cycle.Nodes[0].DisplayName, StringComparer.Ordinal)
            .ThenBy(static cycle => cycle.Nodes[0].Id, StringComparer.Ordinal)
            .ToArray();

        void StartTraversal(string nodeId)
        {
            indexByNodeId.Add(nodeId, index);
            lowLinkByNodeId.Add(nodeId, index);
            index++;
            componentStack.Push(nodeId);
            onComponentStack.Add(nodeId);
            traversalStack.Push(new TraversalFrame(nodeId, adjacency[nodeId]));
        }

        void AddComponent(string rootNodeId)
        {
            var component = new List<GraphNode>();
            string currentNodeId;
            do
            {
                currentNodeId = componentStack.Pop();
                onComponentStack.Remove(currentNodeId);
                component.Add(nodeLookup[currentNodeId]);
            }
            while (!string.Equals(currentNodeId, rootNodeId, StringComparison.Ordinal));

            var hasSelfReference = adjacency[rootNodeId].Contains(rootNodeId, StringComparer.Ordinal);
            if (component.Count > 1 || hasSelfReference)
            {
                cycles.Add(new DependencyCycle
                {
                    Nodes = component
                        .OrderBy(static graphNode => graphNode.DisplayName, StringComparer.Ordinal)
                        .ThenBy(static graphNode => graphNode.Id, StringComparer.Ordinal)
                        .ToArray(),
                });
            }
        }
    }

    private static Dictionary<string, string[]> CreateAdjacency(
        IReadOnlyList<GraphNode> nodes,
        IReadOnlyList<GraphEdge> edges,
        Dictionary<string, GraphNode> nodeLookup,
        CancellationToken cancellationToken)
    {
        var targetsBySource = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            targetsBySource.Add(node.Id, new HashSet<string>(StringComparer.Ordinal));
        }

        foreach (var edge in edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (targetsBySource.TryGetValue(edge.FromId, out var targets) && nodeLookup.ContainsKey(edge.ToId))
            {
                targets.Add(edge.ToId);
            }
        }

        var adjacency = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (sourceId, targets) in targetsBySource)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var orderedTargets = targets
                .OrderBy(targetId => nodeLookup[targetId].DisplayName, StringComparer.Ordinal)
                .ThenBy(static targetId => targetId, StringComparer.Ordinal)
                .ToArray();

            adjacency.Add(sourceId, orderedTargets);
        }

        return adjacency;
    }

    private sealed class TraversalFrame
    {
        private readonly IReadOnlyList<string> _adjacentNodeIds;
        private int _nextIndex;

        public string NodeId { get; }

        public TraversalFrame(string nodeId, IReadOnlyList<string> adjacentNodeIds)
        {
            NodeId = nodeId;
            _adjacentNodeIds = adjacentNodeIds;
        }

        public bool TryGetNext([NotNullWhen(true)] out string? nodeId)
        {
            if (_nextIndex == _adjacentNodeIds.Count)
            {
                nodeId = null;
                return false;
            }

            nodeId = _adjacentNodeIds[_nextIndex];
            _nextIndex++;
            return true;
        }
    }
}
