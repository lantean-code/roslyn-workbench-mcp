namespace Roslyn.Workbench.Mcp.Plugins.Resolution;

internal sealed class ToolRequestResolver : IToolRequestResolver
{
    public ToolRequestResolver()
    {
    }

    public ToolResolutionResult<Document, TResponse> ResolveDocument<TResponse>(DocumentSelector? selector, IToolExecutionContext context)
    {
        if (selector is null)
        {
            var missingSelectorRejection = PluginExecutionResultFactory.Rejected<TResponse>(
                "InvalidRequest",
                "A document selector is required.");

            return ToolResolutionResult<Document, TResponse>.Rejected(missingSelectorRejection);
        }

        var resolution = context.WorkspaceResolver.ResolveDocument(selector);
        if (resolution.IsResolved)
        {
            return ToolResolutionResult<Document, TResponse>.Resolved(resolution.Value);
        }

        var resolutionRejection = PluginExecutionResultFactory.RejectedFromStatus<TResponse>(
            resolution.Status,
            "Document",
            "document");

        return ToolResolutionResult<Document, TResponse>.Rejected(resolutionRejection);
    }

    public ToolResolutionResult<Project, TResponse> ResolveProject<TResponse>(ProjectSelector? selector, IToolExecutionContext context)
    {
        if (selector is null)
        {
            var missingSelectorRejection = PluginExecutionResultFactory.Rejected<TResponse>(
                "InvalidRequest",
                "A project selector is required.");

            return ToolResolutionResult<Project, TResponse>.Rejected(missingSelectorRejection);
        }

        var resolution = context.WorkspaceResolver.ResolveProject(selector);
        if (resolution.IsResolved)
        {
            return ToolResolutionResult<Project, TResponse>.Resolved(resolution.Value);
        }

        var resolutionRejection = PluginExecutionResultFactory.RejectedFromStatus<TResponse>(
            resolution.Status,
            "Project",
            "project");

        return ToolResolutionResult<Project, TResponse>.Rejected(resolutionRejection);
    }

    public ToolResolutionResult<IReadOnlyList<Document>, TResponse> ResolveDocuments<TResponse>(ScopeSelector? scope, IToolExecutionContext context)
    {
        if (scope is null || scope.Kind == ScopeKind.Solution)
        {
            var documents = context.CurrentSolution.Projects
                .SelectMany(static project => project.Documents)
                .ToArray();

            return ToolResolutionResult<IReadOnlyList<Document>, TResponse>.Resolved(documents);
        }

        if (scope.Kind == ScopeKind.Document)
        {
            var documentResolution = ResolveDocument<TResponse>(scope.Document, context);
            if (documentResolution.HasRejection)
            {
                return ToolResolutionResult<IReadOnlyList<Document>, TResponse>.Rejected(documentResolution.Rejection);
            }

            return ToolResolutionResult<IReadOnlyList<Document>, TResponse>.Resolved([documentResolution.Value]);
        }

        var projects = ResolveProjects<TResponse>(scope, context);
        if (projects.HasRejection)
        {
            return ToolResolutionResult<IReadOnlyList<Document>, TResponse>.Rejected(projects.Rejection);
        }

        var resolvedDocuments = projects.Value
            .SelectMany(static project => project.Documents)
            .ToArray();

        return ToolResolutionResult<IReadOnlyList<Document>, TResponse>.Resolved(resolvedDocuments);
    }

    public ToolResolutionResult<IReadOnlyList<Project>, TResponse> ResolveProjects<TResponse>(ScopeSelector? scope, IToolExecutionContext context)
    {
        if (scope is null || scope.Kind == ScopeKind.Solution)
        {
            var solutionProjects = context.CurrentSolution.Projects
                .OrderBy(static project => project.Name, StringComparer.Ordinal)
                .ToArray();

            return ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Resolved(solutionProjects);
        }

        if (scope.Kind == ScopeKind.Document)
        {
            var documentResolution = ResolveDocument<TResponse>(scope.Document, context);
            if (documentResolution.HasRejection)
            {
                return ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Rejected(documentResolution.Rejection);
            }

            return ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Resolved([documentResolution.Value.Project]);
        }

        if (scope.Kind == ScopeKind.Project)
        {
            var projectResolution = ResolveProject<TResponse>(scope.Project, context);
            if (projectResolution.HasRejection)
            {
                return ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Rejected(projectResolution.Rejection);
            }

            return ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Resolved([projectResolution.Value]);
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

        var resolvedProjects = projects
            .DistinctBy(static project => project.Id)
            .OrderBy(static project => project.Name, StringComparer.Ordinal)
            .ToArray();

        return ToolResolutionResult<IReadOnlyList<Project>, TResponse>.Resolved(resolvedProjects);
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
            var missingSelectorRejection = PluginExecutionResultFactory.Rejected<TResponse>(
                "InvalidRequest",
                "A symbol selector is required.");

            return ToolResolutionResult<ISymbol, TResponse>.Rejected(missingSelectorRejection);
        }

        var resolution = await context.WorkspaceResolver.ResolveSymbolAsync(selector, cancellationToken);
        if (resolution.IsResolved)
        {
            return ToolResolutionResult<ISymbol, TResponse>.Resolved(resolution.Value);
        }

        var resolutionRejection = PluginExecutionResultFactory.RejectedFromStatus<TResponse>(
            resolution.Status,
            "Symbol",
            "symbol");

        return ToolResolutionResult<ISymbol, TResponse>.Rejected(resolutionRejection);
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
