using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class DefaultToolRequestResolver : IToolRequestResolver
{
    private readonly IToolResultShaper _resultShaper;

    public DefaultToolRequestResolver(IToolResultShaper resultShaper)
    {
        _resultShaper = resultShaper ?? throw new ArgumentNullException(nameof(resultShaper));
    }

    public ToolResolutionResult<Document, TResponse> ResolveDocument<TResponse>(DocumentSelector? selector, IToolExecutionContext context)
    {
        if (selector is null)
        {
            return new ToolResolutionResult<Document, TResponse>
            {
                Rejection = _resultShaper.Rejected<TResponse>("InvalidRequest", "A document selector is required."),
            };
        }

        var resolution = context.WorkspaceResolver.ResolveDocument(selector);
        return resolution.Status == SelectorResolveStatus.Resolved
            ? new ToolResolutionResult<Document, TResponse> { Value = resolution.Value! }
            : new ToolResolutionResult<Document, TResponse> { Rejection = _resultShaper.RejectFromStatus<TResponse>(resolution.Status, "Document") };
    }

    public ToolResolutionResult<Project, TResponse> ResolveProject<TResponse>(ProjectSelector? selector, IToolExecutionContext context)
    {
        if (selector is null)
        {
            return new ToolResolutionResult<Project, TResponse>
            {
                Rejection = _resultShaper.Rejected<TResponse>("InvalidRequest", "A project selector is required."),
            };
        }

        var resolution = context.WorkspaceResolver.ResolveProject(selector);
        return resolution.Status == SelectorResolveStatus.Resolved
            ? new ToolResolutionResult<Project, TResponse> { Value = resolution.Value! }
            : new ToolResolutionResult<Project, TResponse> { Rejection = _resultShaper.RejectFromStatus<TResponse>(resolution.Status, "Project") };
    }

    public ToolResolutionResult<IReadOnlyList<Document>, TResponse> ResolveDocuments<TResponse>(ScopeSelector? scope, IToolExecutionContext context)
    {
        if (scope is null || scope.Kind == ScopeKind.Solution)
        {
            return new ToolResolutionResult<IReadOnlyList<Document>, TResponse>
            {
                Value = context.CurrentSolution.Projects.SelectMany(static project => project.Documents).ToArray(),
            };
        }

        if (scope.Kind == ScopeKind.Document)
        {
            var documentResolution = ResolveDocument<TResponse>(scope.Document, context);
            return documentResolution.HasRejection
                ? new ToolResolutionResult<IReadOnlyList<Document>, TResponse> { Rejection = documentResolution.Rejection }
                : new ToolResolutionResult<IReadOnlyList<Document>, TResponse> { Value = [documentResolution.Value] };
        }

        var projects = ResolveProjects<TResponse>(scope, context);
        return projects.HasRejection
            ? new ToolResolutionResult<IReadOnlyList<Document>, TResponse> { Rejection = projects.Rejection }
            : new ToolResolutionResult<IReadOnlyList<Document>, TResponse>
            {
                Value = projects.Value
                    .SelectMany(static project => project.Documents)
                    .ToArray(),
            };
    }

    public ToolResolutionResult<IReadOnlyList<Project>, TResponse> ResolveProjects<TResponse>(ScopeSelector? scope, IToolExecutionContext context)
    {
        if (scope is null || scope.Kind == ScopeKind.Solution)
        {
            return new ToolResolutionResult<IReadOnlyList<Project>, TResponse>
            {
                Value = context.CurrentSolution.Projects.OrderBy(static project => project.Name, StringComparer.Ordinal).ToArray(),
            };
        }

        if (scope.Kind == ScopeKind.Document)
        {
            var documentResolution = ResolveDocument<TResponse>(scope.Document, context);
            return documentResolution.HasRejection
                ? new ToolResolutionResult<IReadOnlyList<Project>, TResponse> { Rejection = documentResolution.Rejection }
                : new ToolResolutionResult<IReadOnlyList<Project>, TResponse> { Value = [documentResolution.Value.Project] };
        }

        if (scope.Kind == ScopeKind.Project)
        {
            var projectResolution = ResolveProject<TResponse>(scope.Project, context);
            return projectResolution.HasRejection
                ? new ToolResolutionResult<IReadOnlyList<Project>, TResponse> { Rejection = projectResolution.Rejection }
                : new ToolResolutionResult<IReadOnlyList<Project>, TResponse> { Value = [projectResolution.Value] };
        }

        var projects = new List<Project>();
        foreach (var selector in scope.Projects ?? [])
        {
            var projectResolution = ResolveProject<TResponse>(selector, context);
            if (projectResolution.HasRejection)
            {
                return new ToolResolutionResult<IReadOnlyList<Project>, TResponse> { Rejection = projectResolution.Rejection };
            }

            projects.Add(projectResolution.Value);
        }

        return new ToolResolutionResult<IReadOnlyList<Project>, TResponse>
        {
            Value = projects
                .DistinctBy(static project => project.Id)
                .OrderBy(static project => project.Name, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    public async ValueTask<ToolResolutionResult<ISymbol, TResponse>> ResolveSymbolAsync<TResponse>(
        SymbolSelector? selector,
        SnapshotPrecondition? expectedSnapshot,
        IToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var snapshotRejection = selector?.Location is not null ? ValidateSnapshot<TResponse>(context, expectedSnapshot) : null;
        if (snapshotRejection is not null)
        {
            return new ToolResolutionResult<ISymbol, TResponse>
            {
                Rejection = snapshotRejection,
            };
        }

        if (selector is null)
        {
            return new ToolResolutionResult<ISymbol, TResponse>
            {
                Rejection = _resultShaper.Rejected<TResponse>("InvalidRequest", "A symbol selector is required."),
            };
        }

        var resolution = await context.WorkspaceResolver.ResolveSymbolAsync(selector, cancellationToken).ConfigureAwait(false);
        return resolution.Status == SelectorResolveStatus.Resolved
            ? new ToolResolutionResult<ISymbol, TResponse> { Value = resolution.Value! }
            : new ToolResolutionResult<ISymbol, TResponse> { Rejection = _resultShaper.RejectFromStatus<TResponse>(resolution.Status, "Symbol") };
    }

    public PluginExecutionResult<TResponse>? ValidateSnapshot<TResponse>(IToolExecutionContext context, SnapshotPrecondition? expectedSnapshot)
    {
        var result = context.WorkspaceResolver.ValidateSnapshot(expectedSnapshot);
        return result.Kind == SnapshotMatchKind.Matched
            ? null
            : PluginExecutionResult<TResponse>.Conflict(
                new ToolError
                {
                    Code = "SnapshotMismatch",
                    Message = "The request snapshot does not match the current workspace snapshot.",
                },
                RequiredAction.ResolveTargetAgain);
    }
}
