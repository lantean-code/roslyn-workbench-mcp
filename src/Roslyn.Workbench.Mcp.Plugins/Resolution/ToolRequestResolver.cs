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
            var missingSelectorRejection = PluginExecutionResult.Rejected<TResponse>(
                "InvalidRequest",
                "A document selector is required.");

            return ToolResolutionResult.Rejected<Document, TResponse>(missingSelectorRejection);
        }

        var resolution = context.WorkspaceResolver.ResolveDocument(selector);
        if (resolution.IsResolved)
        {
            return ToolResolutionResult.Resolved<Document, TResponse>(resolution.Value);
        }

        var resolutionRejection = SelectorRejectionFactory.Create<TResponse>(
            resolution.Status,
            "Document",
            "document");

        return ToolResolutionResult.Rejected<Document, TResponse>(resolutionRejection);
    }

    public ToolResolutionResult<Project, TResponse> ResolveProject<TResponse>(ProjectSelector? selector, IToolExecutionContext context)
    {
        if (selector is null)
        {
            var missingSelectorRejection = PluginExecutionResult.Rejected<TResponse>(
                "InvalidRequest",
                "A project selector is required.");

            return ToolResolutionResult.Rejected<Project, TResponse>(missingSelectorRejection);
        }

        var resolution = context.WorkspaceResolver.ResolveProject(selector);
        if (resolution.IsResolved)
        {
            return ToolResolutionResult.Resolved<Project, TResponse>(resolution.Value);
        }

        var resolutionRejection = SelectorRejectionFactory.Create<TResponse>(
            resolution.Status,
            "Project",
            "project");

        return ToolResolutionResult.Rejected<Project, TResponse>(resolutionRejection);
    }

    public ToolResolutionResult<IReadOnlyList<Document>, TResponse> ResolveDocuments<TResponse>(ScopeSelector? scope, IToolExecutionContext context)
    {
        if (scope is null || scope.Kind == ScopeKind.Solution)
        {
            var documents = context.CurrentSolution.Projects
                .SelectMany(static project => project.Documents)
                .ToArray();

            return ToolResolutionResult.Resolved<IReadOnlyList<Document>, TResponse>(documents);
        }

        if (scope.Kind == ScopeKind.Document)
        {
            var documentResolution = ResolveDocument<TResponse>(scope.Document, context);
            if (documentResolution.HasRejection)
            {
                return ToolResolutionResult.Rejected<IReadOnlyList<Document>, TResponse>(documentResolution.Rejection);
            }

            return ToolResolutionResult.Resolved<IReadOnlyList<Document>, TResponse>([documentResolution.Value]);
        }

        var projects = ResolveProjects<TResponse>(scope, context);
        if (projects.HasRejection)
        {
            return ToolResolutionResult.Rejected<IReadOnlyList<Document>, TResponse>(projects.Rejection);
        }

        var resolvedDocuments = projects.Value
            .SelectMany(static project => project.Documents)
            .ToArray();

        return ToolResolutionResult.Resolved<IReadOnlyList<Document>, TResponse>(resolvedDocuments);
    }

    public ToolResolutionResult<IReadOnlyList<Project>, TResponse> ResolveProjects<TResponse>(ScopeSelector? scope, IToolExecutionContext context)
    {
        if (scope is null || scope.Kind == ScopeKind.Solution)
        {
            var solutionProjects = context.CurrentSolution.Projects
                .OrderBy(static project => project.Name, StringComparer.Ordinal)
                .ToArray();

            return ToolResolutionResult.Resolved<IReadOnlyList<Project>, TResponse>(solutionProjects);
        }

        if (scope.Kind == ScopeKind.Document)
        {
            var documentResolution = ResolveDocument<TResponse>(scope.Document, context);
            if (documentResolution.HasRejection)
            {
                return ToolResolutionResult.Rejected<IReadOnlyList<Project>, TResponse>(documentResolution.Rejection);
            }

            return ToolResolutionResult.Resolved<IReadOnlyList<Project>, TResponse>([documentResolution.Value.Project]);
        }

        if (scope.Kind == ScopeKind.Project)
        {
            var projectResolution = ResolveProject<TResponse>(scope.Project, context);
            if (projectResolution.HasRejection)
            {
                return ToolResolutionResult.Rejected<IReadOnlyList<Project>, TResponse>(projectResolution.Rejection);
            }

            return ToolResolutionResult.Resolved<IReadOnlyList<Project>, TResponse>([projectResolution.Value]);
        }

        var projects = new List<Project>();
        foreach (var selector in scope.Projects ?? [])
        {
            var projectResolution = ResolveProject<TResponse>(selector, context);
            if (projectResolution.HasRejection)
            {
                return ToolResolutionResult.Rejected<IReadOnlyList<Project>, TResponse>(projectResolution.Rejection);
            }

            projects.Add(projectResolution.Value);
        }

        var resolvedProjects = projects
            .DistinctBy(static project => project.Id)
            .OrderBy(static project => project.Name, StringComparer.Ordinal)
            .ToArray();

        return ToolResolutionResult.Resolved<IReadOnlyList<Project>, TResponse>(resolvedProjects);
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
            return ToolResolutionResult.Rejected<ISymbol, TResponse>(snapshotRejection);
        }

        if (selector is null)
        {
            var missingSelectorRejection = PluginExecutionResult.Rejected<TResponse>(
                "InvalidRequest",
                "A symbol selector is required.");

            return ToolResolutionResult.Rejected<ISymbol, TResponse>(missingSelectorRejection);
        }

        var resolution = await context.WorkspaceResolver.ResolveSymbolAsync(selector, cancellationToken);
        if (resolution.IsResolved)
        {
            return ToolResolutionResult.Resolved<ISymbol, TResponse>(resolution.Value);
        }

        var resolutionRejection = SelectorRejectionFactory.Create<TResponse>(
            resolution.Status,
            "Symbol",
            "symbol");

        return ToolResolutionResult.Rejected<ISymbol, TResponse>(resolutionRejection);
    }

    public PluginExecutionResult<TResponse>? ValidateSnapshot<TResponse>(IToolExecutionContext context, SnapshotPrecondition? expectedSnapshot)
    {
        var result = context.WorkspaceResolver.ValidateSnapshot(expectedSnapshot);
        return result.Kind == SnapshotMatchKind.Matched
            ? null
            : PluginExecutionResult.Conflict<TResponse>(
                new PluginExecutionError
                {
                    Code = "SnapshotMismatch",
                    Message = "The request snapshot does not match the current workspace snapshot.",
                },
                RequiredAction.ResolveTargetAgain);
    }
}
