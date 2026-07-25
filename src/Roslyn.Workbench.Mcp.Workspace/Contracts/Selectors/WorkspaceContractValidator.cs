namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

internal static class WorkspaceContractValidator
{
    public static IReadOnlyList<string> Validate(DocumentSelector selector)
    {
        var errors = new List<string>();
        if (CountProvided(selector.Path, selector.DocumentId) != 1)
        {
            errors.Add("DocumentSelector must provide exactly one of Path or DocumentId.");
        }

        if (selector.Project is not null)
        {
            errors.AddRange(Validate(selector.Project));
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(ProjectSelector selector)
    {
        return CountProvided(selector.ProjectId, selector.Name, selector.Path, selector.TargetFramework) >= 1
            ? []
            : ["ProjectSelector must provide at least one of ProjectId, Name, Path, or TargetFramework."];
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

    public static bool IsWithinDocument(TextSpanSelector selector, int documentLength)
    {
        return selector.Start >= 0
            && selector.Length >= 0
            && selector.Start <= documentLength - selector.Length;
    }

    public static IReadOnlyList<string> Validate(SymbolSelector selector)
    {
        var errors = new List<string>();

        if (CountProvided(selector.Location, selector.DocumentationCommentId) != 1)
        {
            errors.Add("SymbolSelector must provide exactly one of Location or DocumentationCommentId.");
        }

        if (selector.Project is not null)
        {
            errors.AddRange(Validate(selector.Project));
        }

        return errors;
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
