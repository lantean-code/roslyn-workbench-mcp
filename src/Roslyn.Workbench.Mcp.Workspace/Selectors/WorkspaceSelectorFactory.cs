namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Builds canonical location and symbol selectors from resolved source locations.
/// </summary>
internal sealed class WorkspaceSelectorFactory : IWorkspaceSelectorFactory
{
    /// <summary>
    /// Creates a canonical location selector for a resolved source span.
    /// </summary>
    /// <param name="resolvedLocation">The resolved source location from which to create a canonical selector.</param>
    /// <returns>The created canonical location selector.</returns>
    public CanonicalLocationSelector? CreateCanonicalLocationSelector(ResolvedLocation? resolvedLocation)
    {
        var spanSelector = CreateTextSpanSelector(resolvedLocation);
        if (spanSelector is null)
        {
            return null;
        }

        return new CanonicalLocationSelector
        {
            Span = spanSelector,
        };
    }

    /// <summary>
    /// Creates a location selector for a resolved source span.
    /// </summary>
    /// <param name="resolvedLocation">The resolved source location from which to create a canonical selector.</param>
    /// <returns>The created location selector.</returns>
    public LocationSelector? CreateLocationSelector(ResolvedLocation? resolvedLocation)
    {
        var spanSelector = CreateTextSpanSelector(resolvedLocation);
        if (spanSelector is null)
        {
            return null;
        }

        return new LocationSelector
        {
            Span = spanSelector,
        };
    }

    /// <summary>
    /// Creates a symbol selector anchored to a resolved source span.
    /// </summary>
    /// <param name="resolvedLocation">The resolved source location from which to create a canonical selector.</param>
    /// <returns>The created symbol selector.</returns>
    public SymbolSelector? CreateSymbolSelector(ResolvedLocation? resolvedLocation)
    {
        var locationSelector = CreateLocationSelector(resolvedLocation);
        if (locationSelector is null)
        {
            return null;
        }

        return new SymbolSelector
        {
            Location = locationSelector,
        };
    }

    private static TextSpanSelector? CreateTextSpanSelector(ResolvedLocation? resolvedLocation)
    {
        if (resolvedLocation?.Document is not { } document
            || resolvedLocation.Span is not { } resolvedSpan)
        {
            return null;
        }

        var documentSelector = CreateDocumentSelector(document);
        var range = new TextSpanRange
        {
            Start = resolvedSpan.Start,
            Length = resolvedSpan.Length,
        };

        return new TextSpanSelector
        {
            Document = documentSelector,
            Range = range,
        };
    }

    private static DocumentSelector CreateDocumentSelector(DocumentReference document)
    {
        var projectSelector = CreateProjectSelector(document);
        if (!string.IsNullOrWhiteSpace(document.DocumentId))
        {
            return new DocumentSelector
            {
                DocumentId = document.DocumentId,
                Project = projectSelector,
            };
        }

        return new DocumentSelector
        {
            Path = document.Path,
            Project = projectSelector,
        };
    }

    private static ProjectSelector? CreateProjectSelector(DocumentReference document)
    {
        if (string.IsNullOrWhiteSpace(document.ProjectId))
        {
            return null;
        }

        return new ProjectSelector
        {
            ProjectId = document.ProjectId,
        };
    }
}
