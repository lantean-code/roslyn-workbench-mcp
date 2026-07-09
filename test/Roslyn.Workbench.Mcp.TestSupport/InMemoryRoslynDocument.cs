namespace Roslyn.Workbench.Mcp.TestSupport;

/// <summary>
/// Represents a single in-memory Roslyn document and its owning Roslyn solution for unit tests.
/// </summary>
public sealed class InMemoryRoslynDocument : IDisposable
{
    internal InMemoryRoslynDocument(AdhocWorkspace workspace, Solution solution, Document document)
    {
        Workspace = workspace;
        Solution = solution;
        Document = document;
    }

    /// <summary>
    /// Gets the workspace that owns the in-memory solution.
    /// </summary>
    public AdhocWorkspace Workspace { get; }

    /// <summary>
    /// Gets the solution that contains the in-memory document.
    /// </summary>
    public Solution Solution { get; }

    /// <summary>
    /// Gets the in-memory Roslyn document.
    /// </summary>
    public Document Document { get; }

    /// <summary>
    /// Resolves the location for a single syntax node that matches the supplied predicate.
    /// </summary>
    /// <typeparam name="TNode">The syntax node type to search for.</typeparam>
    /// <param name="predicate">The predicate used to select the target node.</param>
    /// <returns>The Roslyn location for the matching node.</returns>
    public Location GetSingleNodeLocation<TNode>(Func<TNode, bool> predicate)
        where TNode : SyntaxNode
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return RoslynDocumentTestHelper
            .GetSingleNodeLocationAsync(Document, predicate, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Disposes the underlying Roslyn workspace.
    /// </summary>
    public void Dispose()
    {
        Workspace.Dispose();
    }
}
