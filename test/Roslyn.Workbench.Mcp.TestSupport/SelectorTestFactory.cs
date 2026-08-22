namespace Roslyn.Workbench.Mcp.TestSupport;

/// <summary>
/// Creates reusable contract selector graphs for tests.
/// </summary>
public static class SelectorTestFactory
{
    /// <summary>
    /// Creates an unresolved selector result for tests that vary the unsuccessful status.
    /// </summary>
    /// <typeparam name="T">The unresolved value type.</typeparam>
    /// <param name="status">The not-found, ambiguous or invalid status.</param>
    /// <returns>The unresolved selector result.</returns>
    public static SelectorResolveResult<T> CreateUnresolvedResult<T>(SelectorResolveStatus status)
        where T : class
    {
        if (status == SelectorResolveStatus.NotFound)
        {
            return SelectorResolveResult.NotFound<T>();
        }

        if (status == SelectorResolveStatus.Ambiguous)
        {
            return SelectorResolveResult.Ambiguous<T>();
        }

        if (status == SelectorResolveStatus.Invalid)
        {
            return SelectorResolveResult.Invalid<T>();
        }

        throw new ArgumentOutOfRangeException(nameof(status), status, "An unresolved selector status is required.");
    }

    /// <summary>
    /// Creates a resolved location projection from a Roslyn location.
    /// </summary>
    /// <param name="location">The Roslyn location.</param>
    /// <param name="path">The projected document path.</param>
    /// <param name="documentId">The projected document identifier.</param>
    /// <param name="projectId">The projected project identifier.</param>
    /// <returns>The created resolved location.</returns>
    public static ResolvedLocation CreateResolvedLocation(
        Location location,
        string path,
        string documentId = "DocumentId",
        string projectId = "ProjectId")
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return CreateResolvedLocation(path, location.SourceSpan.Start, location.SourceSpan.Length, documentId, projectId);
    }

    /// <summary>
    /// Creates a resolved location projection from explicit span values.
    /// </summary>
    /// <param name="path">The projected document path.</param>
    /// <param name="start">The projected span start.</param>
    /// <param name="length">The projected span length.</param>
    /// <param name="documentId">The projected document identifier.</param>
    /// <param name="projectId">The projected project identifier.</param>
    /// <returns>The created resolved location.</returns>
    public static ResolvedLocation CreateResolvedLocation(
        string path,
        int start,
        int length,
        string documentId = "DocumentId",
        string projectId = "ProjectId",
        SnapshotPrecondition? snapshot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        var resolvedSnapshot = snapshot ?? WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        return new ResolvedLocation
        {
            Snapshot = resolvedSnapshot,
            Document = new DocumentReference
            {
                DocumentId = documentId,
                ProjectId = projectId,
                Path = path,
            },
            Span = new TextSpanRange
            {
                Start = start,
                Length = length,
            },
        };
    }

    /// <summary>
    /// Creates a span-based location selector.
    /// </summary>
    /// <param name="path">The selected document path.</param>
    /// <param name="start">The zero-based UTF-16 start position.</param>
    /// <param name="length">The zero-based UTF-16 length.</param>
    /// <param name="documentId">The optional selected document identifier.</param>
    /// <returns>The created location selector.</returns>
    public static LocationSelector CreateSpanLocationSelector(
        string path,
        int start,
        int length,
        string? documentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        var document = new DocumentSelector
        {
            Path = path,
            DocumentId = documentId,
        };

        var span = CreateTextSpanSelector(document, start, length);

        return new LocationSelector
        {
            Span = span,
        };
    }

    /// <summary>
    /// Creates a document-bound text span selector.
    /// </summary>
    /// <param name="document">The selected document.</param>
    /// <param name="start">The zero-based UTF-16 start position.</param>
    /// <param name="length">The zero-based UTF-16 length.</param>
    /// <returns>The created text span selector.</returns>
    public static TextSpanSelector CreateTextSpanSelector(DocumentSelector document, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(document);

        var range = new TextSpanRange
        {
            Start = start,
            Length = length,
        };

        return new TextSpanSelector
        {
            Document = document,
            Range = range,
        };
    }

    /// <summary>
    /// Creates a symbol reference projection from a Roslyn symbol.
    /// </summary>
    /// <param name="symbol">The Roslyn symbol.</param>
    /// <param name="location">The optional projected source location.</param>
    /// <returns>The created symbol reference.</returns>
    public static SymbolReference CreateSymbolReference(ISymbol symbol, ResolvedLocation? location = null)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        return CreateSymbolReference(symbol.Name, symbol.Kind, symbol.GetDocumentationCommentId(), location);
    }

    /// <summary>
    /// Creates a symbol reference projection from explicit values.
    /// </summary>
    /// <param name="displayName">The projected display name.</param>
    /// <param name="kind">The projected symbol kind.</param>
    /// <param name="documentationCommentId">The projected documentation comment identifier.</param>
    /// <param name="location">The optional projected source location.</param>
    /// <returns>The created symbol reference.</returns>
    public static SymbolReference CreateSymbolReference(
        string displayName,
        SymbolKind kind,
        string? documentationCommentId = null,
        ResolvedLocation? location = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new SymbolReference
        {
            DisplayName = displayName,
            Kind = kind.ToString(),
            DocumentationCommentId = documentationCommentId,
            Location = location,
        };
    }
}
