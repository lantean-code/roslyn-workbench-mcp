using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.TestSupport;

/// <summary>
/// Represents an in-memory Roslyn solution for unit tests that require real multi-project state.
/// </summary>
public sealed class InMemoryRoslynSolution : IDisposable
{
    private readonly ImmutableDictionary<string, ProjectId> _projectIdsByName;

    internal InMemoryRoslynSolution(
        AdhocWorkspace workspace,
        Solution solution,
        ImmutableDictionary<string, ProjectId> projectIdsByName)
    {
        Workspace = workspace;
        Solution = solution;
        _projectIdsByName = projectIdsByName;
    }

    /// <summary>
    /// Gets the workspace that owns the in-memory solution.
    /// </summary>
    public AdhocWorkspace Workspace { get; }

    /// <summary>
    /// Gets the in-memory Roslyn solution.
    /// </summary>
    public Solution Solution { get; }

    /// <summary>
    /// Gets a project by its logical name.
    /// </summary>
    /// <param name="projectName">The logical project name.</param>
    /// <returns>The resolved Roslyn project.</returns>
    public Project GetProject(string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        if (!_projectIdsByName.TryGetValue(projectName, out var projectId))
        {
            throw new InvalidOperationException($"The project '{projectName}' is not part of this solution.");
        }

        return Solution.GetProject(projectId) ?? throw new InvalidOperationException($"The project '{projectName}' could not be resolved.");
    }

    /// <summary>
    /// Gets a document by name, optionally constrained to a specific project.
    /// </summary>
    /// <param name="documentName">The logical document name.</param>
    /// <param name="projectName">The optional logical project name.</param>
    /// <returns>The resolved Roslyn document.</returns>
    public Document GetDocument(string documentName, string? projectName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        if (projectName is not null)
        {
            var project = GetProject(projectName);
            return project.Documents.Single(item => string.Equals(item.Name, documentName, StringComparison.Ordinal));
        }

        var documents = Solution.Projects
            .SelectMany(static item => item.Documents)
            .Where(item => string.Equals(item.Name, documentName, StringComparison.Ordinal))
            .ToArray();

        return documents.Length switch
        {
            0 => throw new InvalidOperationException($"The document '{documentName}' is not part of this solution."),
            1 => documents[0],
            _ => throw new InvalidOperationException($"The document '{documentName}' exists in multiple projects. Specify a project name."),
        };
    }

    /// <summary>
    /// Resolves the location for a single syntax node in a named document that matches the supplied predicate.
    /// </summary>
    /// <typeparam name="TNode">The syntax node type to search for.</typeparam>
    /// <param name="documentName">The logical document name.</param>
    /// <param name="predicate">The predicate used to select the target node.</param>
    /// <param name="projectName">The optional logical project name.</param>
    /// <returns>The Roslyn location for the matching node.</returns>
    public Location GetSingleNodeLocation<TNode>(string documentName, Func<TNode, bool> predicate, string? projectName = null)
        where TNode : SyntaxNode
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var document = GetDocument(documentName, projectName);
        var syntaxRoot = document.GetSyntaxRootAsync().GetAwaiter().GetResult()
            ?? throw new InvalidOperationException($"The syntax root for '{document.Name}' could not be resolved.");
        var node = syntaxRoot
            .DescendantNodes()
            .OfType<TNode>()
            .Single(predicate);

        return node.GetLocation();
    }

    /// <summary>
    /// Disposes the underlying Roslyn workspace.
    /// </summary>
    public void Dispose()
    {
        Workspace.Dispose();
    }
}
