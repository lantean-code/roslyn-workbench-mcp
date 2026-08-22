namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

internal sealed class WorkspaceSelectorFactory : IWorkspaceSelectorFactory
{
    public LocationSelector? CreateLocationSelector(ResolvedLocation? resolvedLocation)
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

        var spanSelector = new TextSpanSelector
        {
            Document = documentSelector,
            Range = range,
        };

        return new LocationSelector
        {
            Span = spanSelector,
        };
    }

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
