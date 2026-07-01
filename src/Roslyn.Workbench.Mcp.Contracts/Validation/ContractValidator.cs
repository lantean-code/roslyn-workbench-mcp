using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Validation;

/// <summary>
/// Provides non-throwing validation helpers for shared contract invariants.
/// </summary>
public static class ContractValidator
{
    /// <summary>
    /// Validates a document selector.
    /// </summary>
    /// <param name="selector">The selector to validate.</param>
    /// <returns>The validation errors, if any.</returns>
    public static IReadOnlyList<string> Validate(DocumentSelector selector)
    {
        var count = CountProvided(selector.Path, selector.DocumentId);

        if (count == 1)
        {
            return [];
        }

        return ["DocumentSelector must provide exactly one of Path or DocumentId."];
    }

    /// <summary>
    /// Validates a project selector.
    /// </summary>
    /// <param name="selector">The selector to validate.</param>
    /// <returns>The validation errors, if any.</returns>
    public static IReadOnlyList<string> Validate(ProjectSelector selector)
    {
        if (CountProvided(selector.ProjectId, selector.Name, selector.Path) >= 1)
        {
            return [];
        }

        return ["ProjectSelector must provide at least one of ProjectId, Name, or Path."];
    }

    /// <summary>
    /// Validates a workspace selector.
    /// </summary>
    /// <param name="selector">The selector to validate.</param>
    /// <returns>The validation errors, if any.</returns>
    public static IReadOnlyList<string> Validate(WorkspaceSelector selector)
    {
        if (CountProvided(selector.WorkspaceId, selector.Alias, selector.Path) >= 1)
        {
            return [];
        }

        return ["WorkspaceSelector must provide at least one of WorkspaceId, Alias, or Path."];
    }

    /// <summary>
    /// Validates a location selector.
    /// </summary>
    /// <param name="selector">The selector to validate.</param>
    /// <returns>The validation errors, if any.</returns>
    public static IReadOnlyList<string> Validate(LocationSelector selector)
    {
        var count = CountProvided(selector.Span, selector.Selection);

        if (count == 1)
        {
            return [];
        }

        return ["LocationSelector must provide exactly one of Span or Selection."];
    }

    /// <summary>
    /// Validates a symbol selector.
    /// </summary>
    /// <param name="selector">The selector to validate.</param>
    /// <returns>The validation errors, if any.</returns>
    public static IReadOnlyList<string> Validate(SymbolSelector selector)
    {
        var count = CountProvided(selector.Location, selector.DocumentationCommentId);

        if (count == 1)
        {
            return [];
        }

        return ["SymbolSelector must provide exactly one of Location or DocumentationCommentId."];
    }

    /// <summary>
    /// Validates a scope selector.
    /// </summary>
    /// <param name="selector">The selector to validate.</param>
    /// <returns>The validation errors, if any.</returns>
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

    /// <summary>
    /// Validates a result limit.
    /// </summary>
    /// <param name="limit">The limit to validate.</param>
    /// <returns>The validation errors, if any.</returns>
    public static IReadOnlyList<string> Validate(ResultLimit limit)
    {
        if (limit.MaxResults is null || limit.MaxResults >= 1)
        {
            return [];
        }

        return ["ResultLimit MaxResults must be at least 1 when provided."];
    }

    /// <summary>
    /// Validates a tool result.
    /// </summary>
    /// <typeparam name="TData">The tool-specific payload type.</typeparam>
    /// <param name="result">The result to validate.</param>
    /// <returns>The validation errors, if any.</returns>
    public static IReadOnlyList<string> Validate<TData>(ToolResult<TData> result)
    {
        var errors = new List<string>();

        switch (result.Outcome)
        {
            case ToolOutcome.Succeeded:
                if (result.Data is null)
                {
                    errors.Add("ToolResult Succeeded outcome requires Data.");
                }
                break;

            case ToolOutcome.NoChange:
                if (result.Changes is not null)
                {
                    errors.Add("ToolResult NoChange outcome must not include Changes.");
                }

                if (result.Error is not null)
                {
                    errors.Add("ToolResult NoChange outcome must not include Error.");
                }
                break;

            case ToolOutcome.Rejected:
            case ToolOutcome.Conflict:
            case ToolOutcome.Faulted:
                if (result.Error is null)
                {
                    errors.Add($"ToolResult {result.Outcome} outcome requires Error.");
                }

                if (result.Data is not null)
                {
                    errors.Add($"ToolResult {result.Outcome} outcome must not include Data.");
                }

                if (result.Changes is not null)
                {
                    errors.Add($"ToolResult {result.Outcome} outcome must not include Changes.");
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
