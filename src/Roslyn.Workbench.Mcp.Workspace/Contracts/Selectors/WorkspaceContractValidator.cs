namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

internal static class WorkspaceContractValidator
{
    public static IReadOnlyList<string> Validate(DocumentSelector selector)
    {
        return CountProvided(selector.Path, selector.DocumentId) == 1
            ? []
            : ["DocumentSelector must provide exactly one of Path or DocumentId."];
    }

    public static IReadOnlyList<string> Validate(ProjectSelector selector)
    {
        return CountProvided(selector.ProjectId, selector.Name, selector.Path) >= 1
            ? []
            : ["ProjectSelector must provide at least one of ProjectId, Name, or Path."];
    }

    public static IReadOnlyList<string> Validate(WorkspaceSelector selector)
    {
        return CountProvided(selector.WorkspaceId, selector.Alias, selector.Path) >= 1
            ? []
            : ["WorkspaceSelector must provide at least one of WorkspaceId, Alias, or Path."];
    }

    public static IReadOnlyList<string> Validate(LocationSelector selector)
    {
        return CountProvided(selector.Span, selector.Selection) == 1
            ? []
            : ["LocationSelector must provide exactly one of Span or Selection."];
    }

    public static IReadOnlyList<string> Validate(SymbolSelector selector)
    {
        return CountProvided(selector.Location, selector.DocumentationCommentId) == 1
            ? []
            : ["SymbolSelector must provide exactly one of Location or DocumentationCommentId."];
    }

    public static IReadOnlyList<string> Validate(ScopeSelector selector)
    {
        var errors = new List<string>();

        switch (selector.Kind)
        {
            case ScopeKind.Solution:
                if (selector.Project is not null || selector.Document is not null || selector.Projects is not null)
                {
                    errors.Add("ScopeSelector Kind Solution must not provide Project, Document, or Projects.");
                }
                break;

            case ScopeKind.Project:
                if (selector.Project is null)
                {
                    errors.Add("ScopeSelector Kind Project must provide Project.");
                }

                if (selector.Document is not null || selector.Projects is not null)
                {
                    errors.Add("ScopeSelector Kind Project must not provide Document or Projects.");
                }
                break;

            case ScopeKind.Document:
                if (selector.Document is null)
                {
                    errors.Add("ScopeSelector Kind Document must provide Document.");
                }

                if (selector.Project is not null || selector.Projects is not null)
                {
                    errors.Add("ScopeSelector Kind Document must not provide Project or Projects.");
                }
                break;

            case ScopeKind.Projects:
                if (selector.Projects is null || selector.Projects.Count == 0)
                {
                    errors.Add("ScopeSelector Kind Projects must provide at least one Project selector.");
                }

                if (selector.Project is not null || selector.Document is not null)
                {
                    errors.Add("ScopeSelector Kind Projects must not provide Project or Document.");
                }
                break;
        }

        return errors;
    }

    private static int CountProvided(params object?[] values)
    {
        return values.Count(static value => value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            _ => true,
        });
    }
}
