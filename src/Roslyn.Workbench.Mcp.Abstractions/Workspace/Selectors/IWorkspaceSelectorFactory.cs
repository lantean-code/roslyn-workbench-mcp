namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Creates canonical workspace selectors from resolved workspace identities.
/// </summary>
public interface IWorkspaceSelectorFactory
{
    /// <summary>
    /// Creates a canonical result selector from a resolved location.
    /// </summary>
    /// <param name="resolvedLocation">The resolved location to project.</param>
    /// <returns>
    /// A canonical result selector, or <see langword="null"/> when the resolved location does not contain a document and text span.
    /// </returns>
    CanonicalLocationSelector? CreateCanonicalLocationSelector(ResolvedLocation? resolvedLocation);

    /// <summary>
    /// Creates a location selector from a resolved location.
    /// </summary>
    /// <param name="resolvedLocation">The resolved location to project.</param>
    /// <returns>
    /// A canonical location selector, or <see langword="null"/> when the resolved location does not contain a document and text span.
    /// </returns>
    LocationSelector? CreateLocationSelector(ResolvedLocation? resolvedLocation);

    /// <summary>
    /// Creates a location-backed symbol selector from a resolved location.
    /// </summary>
    /// <param name="resolvedLocation">The resolved location to project.</param>
    /// <returns>
    /// A canonical symbol selector, or <see langword="null"/> when the resolved location does not contain a document and text span.
    /// </returns>
    SymbolSelector? CreateSymbolSelector(ResolvedLocation? resolvedLocation);
}
