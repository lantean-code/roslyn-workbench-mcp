namespace Roslyn.Workbench.Mcp.Plugins.Core.Resolution;

internal sealed class ToolRequestResolver : IToolRequestResolver
{
    public ToolRequestResolver()
    {
    }

    public ToolResolutionResult<Document, TResponse> ResolveDocument<TResponse>(DocumentSelector? selector, IToolExecutionContext context)
    {
        if (selector is null)
        {
            return ToolResolutionResult<Document, TResponse>.Rejected(ToolExecutionHelpers.Rejected<TResponse>("InvalidRequest", "A document selector is required."));
        }

        var resolution = context.WorkspaceResolver.ResolveDocument(selector);
        return resolution.IsResolved
            ? ToolResolutionResult<Document, TResponse>.Resolved(resolution.Value)
            : ToolResolutionResult<Document, TResponse>.Rejected(ToolExecutionHelpers.RejectFromStatus<TResponse>(resolution.Status, "Document", "document"));
    }

    public ToolResolutionResult<Project, TResponse> ResolveProject<TResponse>(ProjectSelector? selector, IToolExecutionContext context)
    {
        if (selector is null)
        {
            return ToolResolutionResult<Project, TResponse>.Rejected(ToolExecutionHelpers.Rejected<TResponse>("InvalidRequest", "A project selector is required."));
        }

        var resolution = context.WorkspaceResolver.ResolveProject(selector);
        return resolution.IsResolved
            ? ToolResolutionResult<Project, TResponse>.Resolved(resolution.Value)
            : ToolResolutionResult<Project, TResponse>.Rejected(ToolExecutionHelpers.RejectFromStatus<TResponse>(resolution.Status, "Project", "project"));
    }

    public ToolResolutionResult<IReadOnlyList<Document>, TResponse> ResolveDocuments<TResponse>(ScopeSelector? scope, IToolExecutionContext context)
    {
        if (scope is null || scope.Kind == ScopeKind.Solution)
        {
            return ToolResolutionResult<IReadOnlyList<Document>, TResponse>.Resolved(context.CurrentSolution.Projects.SelectMany(static project => project.Documents).ToArray());
        }

        if (scope.Kind == ScopeKind.Document)
        {
            var documentResolution = ResolveDocument<TResponse>(scope.Document, context);
            return documentResolution.HasRejection
                ? ToolResolutionResult<IReadOnlyList<Document>, TResponse>.Rejected(documentResolution.Rejection)
                : ToolResolutionResult<IReadOnlyList<Document>, TResponse>.Resolved([documentResolution.Value]);
        }

        var projects = ResolveProjects<TResponse>(scope, context);
        return projects.HasRejection
            ? ToolResolutionResult<IReadOnlyList<Document>, TResponse>.Rejected(projects.Rejection)
            : ToolResolutionResult<IReadOnlyList<Document>, TResponse>.Resolved(projects.Value
                    .SelectMany(static project => project.Documents)
                    .ToArray());
    }

    public ToolResolutionResult<IReadOnlyList<Project>, TResponse> ResolveProjects<TResponse>(ScopeSelector? scope, IToolExecutionContext context)
    {
        if (scope is null || scope.Kind == ScopeKind.Solution)
        {
            return ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Resolved(context.CurrentSolution.Projects.OrderBy(static project => project.Name, StringComparer.Ordinal).ToArray());
        }

        if (scope.Kind == ScopeKind.Document)
        {
            var documentResolution = ResolveDocument<TResponse>(scope.Document, context);
            return documentResolution.HasRejection
                ? ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Rejected(documentResolution.Rejection)
                : ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Resolved([documentResolution.Value.Project]);
        }

        if (scope.Kind == ScopeKind.Project)
        {
            var projectResolution = ResolveProject<TResponse>(scope.Project, context);
            return projectResolution.HasRejection
                ? ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Rejected(projectResolution.Rejection)
                : ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Resolved([projectResolution.Value]);
        }

        var projects = new List<Project>();
        foreach (var selector in scope.Projects ?? [])
        {
            var projectResolution = ResolveProject<TResponse>(selector, context);
            if (projectResolution.HasRejection)
            {
                return ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Rejected(projectResolution.Rejection);
            }

            projects.Add(projectResolution.Value);
        }

        return ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Resolved(projects
                .DistinctBy(static project => project.Id)
                .OrderBy(static project => project.Name, StringComparer.Ordinal)
                .ToArray());
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
            return ToolResolutionResult<ISymbol, TResponse>.Rejected(snapshotRejection);
        }

        if (selector is null)
        {
            return ToolResolutionResult<ISymbol, TResponse>.Rejected(ToolExecutionHelpers.Rejected<TResponse>("InvalidRequest", "A symbol selector is required."));
        }

        var resolution = await context.WorkspaceResolver.ResolveSymbolAsync(selector, cancellationToken);
        return resolution.IsResolved
            ? ToolResolutionResult<ISymbol, TResponse>.Resolved(resolution.Value)
            : ToolResolutionResult<ISymbol, TResponse>.Rejected(ToolExecutionHelpers.RejectFromStatus<TResponse>(resolution.Status, "Symbol", "symbol"));
    }

    public PluginExecutionResult<TResponse>? ValidateSnapshot<TResponse>(IToolExecutionContext context, SnapshotPrecondition? expectedSnapshot)
    {
        var result = context.WorkspaceResolver.ValidateSnapshot(expectedSnapshot);
        return result.Kind == SnapshotMatchKind.Matched
            ? null
            : PluginExecutionResult<TResponse>.Conflict(
                new PluginExecutionError
                {
                    Code = "SnapshotMismatch",
                    Message = "The request snapshot does not match the current workspace snapshot.",
                },
                RequiredAction.ResolveTargetAgain);
    }
}
