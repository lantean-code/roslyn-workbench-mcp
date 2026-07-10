using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

/// <summary>
/// Provides host-owned workspace resolution helpers for plugin execution.
/// </summary>
public interface IWorkspaceResolver
{
    /// <summary>
    /// Normalizes a document path to its workspace-relative form.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized workspace-relative path.</returns>
    string NormalizeDocumentPath(string path);

    /// <summary>
    /// Normalizes a project path to its workspace-relative form.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized workspace-relative path.</returns>
    string NormalizeProjectPath(string path);

    /// <summary>
    /// Resolves a document selector against the current solution.
    /// </summary>
    /// <param name="selector">The selector to resolve.</param>
    /// <returns>The resolved document, when one match exists.</returns>
    SelectorResolveResult<Document> ResolveDocument(DocumentSelector selector);

    /// <summary>
    /// Resolves a location selector against the current solution.
    /// </summary>
    /// <param name="selector">The selector to resolve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved Roslyn location, when one match exists.</returns>
    ValueTask<SelectorResolveResult<Location>> ResolveLocationAsync(LocationSelector selector, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a project selector against the current solution.
    /// </summary>
    /// <param name="selector">The selector to resolve.</param>
    /// <returns>The resolved project, when one match exists.</returns>
    SelectorResolveResult<Project> ResolveProject(ProjectSelector selector);

    /// <summary>
    /// Resolves a symbol selector against the current solution.
    /// </summary>
    /// <param name="selector">The selector to resolve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved symbol, when one match exists.</returns>
    ValueTask<SelectorResolveResult<ISymbol>> ResolveSymbolAsync(SymbolSelector selector, CancellationToken cancellationToken);

    /// <summary>
    /// Validates a snapshot precondition against the current execution snapshot.
    /// </summary>
    /// <param name="precondition">The expected snapshot.</param>
    /// <returns>The snapshot validation result.</returns>
    SnapshotMatchResult ValidateSnapshot(SnapshotPrecondition? precondition);

    /// <summary>
    /// Creates a resolved document reference.
    /// </summary>
    /// <param name="document">The Roslyn document.</param>
    /// <returns>The projected document reference, when it can be represented.</returns>
    DocumentReference? CreateDocumentReference(Document document);

    /// <summary>
    /// Creates a resolved location for the specified source location.
    /// </summary>
    /// <param name="location">The Roslyn location.</param>
    /// <returns>The resolved location, when the source location can be represented.</returns>
    ResolvedLocation? CreateResolvedLocation(Location location);

    /// <summary>
    /// Creates a projected symbol reference.
    /// </summary>
    /// <param name="symbol">The Roslyn symbol.</param>
    /// <returns>The projected symbol reference.</returns>
    SymbolReference CreateSymbolReference(ISymbol symbol);
}
