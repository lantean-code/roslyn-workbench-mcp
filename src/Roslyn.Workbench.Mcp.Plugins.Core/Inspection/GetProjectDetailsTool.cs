namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns project metadata, options and selected document details.
/// </summary>
[RoslynTool("get-project-details", "Get Project Details", "Returns project metadata, options and selected document details.")]
internal sealed class GetProjectDetailsTool : QueryToolHandler<GetProjectDetailsRequest, ProjectDetailsData>
{
    /// <inheritdoc/>
    protected override ValueTask<PluginExecutionResult<ProjectDetailsData>> ExecuteCoreAsync(GetProjectDetailsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var projectResolution = context.ToolExecutionServices.RequestResolver.ResolveProject<ProjectDetailsData>(request.Project, context);
        if (projectResolution.HasRejection)
        {
            return ValueTask.FromResult(projectResolution.Rejection);
        }

        var project = projectResolution.Value;
        var targetFrameworks = context.ToolExecutionServices.ProjectTargetFrameworkResolver.Resolve(
            context.WorkspaceIdentity.WorkspaceId,
            project,
            cancellationToken);

        if (!targetFrameworks.IsSucceeded)
        {
            var rejection = PluginExecutionResult.Rejected<ProjectDetailsData>(
                "ProjectStructureUnavailable",
                targetFrameworks.ErrorMessage,
                RequiredAction.Retry);
            return ValueTask.FromResult(rejection);
        }

        if (!context.WorkspacePathService.TryNormalizePath(project.FilePath ?? project.Name, out var projectPath))
        {
            var rejection = PluginExecutionResult.Rejected<ProjectDetailsData>(
                "ProjectStructureUnavailable",
                "The resolved project's path could not be normalized relative to the workspace root.",
                RequiredAction.ReloadWorkspace);
            return ValueTask.FromResult(rejection);
        }

        BoundedCollection<DocumentReference>? documents = null;
        if (request.IncludeDocuments)
        {
            documents = CreateDocumentReferences(
                context.WorkspaceResolver.GetDocuments(project),
                context.WorkspacePathService,
                context.WorkspaceResolver,
                request.EffectiveDocumentsLimit,
                cancellationToken);
        }

        var projectReferences = CreateProjectReferences(
            project,
            context.CurrentSolution,
            context.WorkspacePathService,
            request.EffectiveProjectReferencesLimit,
            cancellationToken);

        var metadataReferences = CreateMetadataReferences(
            project,
            request.EffectiveMetadataReferencesLimit,
            cancellationToken);

        var analyzers = CreateAnalyzers(
            project,
            request.EffectiveAnalyzersLimit,
            cancellationToken);

        var projectDetails = new ProjectDetailsData
        {
            Project = InspectionProjectionFactory.CreateProjectInfo(project, projectPath, targetFrameworks.TargetFrameworks),
            Documents = documents,
            ProjectReferences = projectReferences,
            MetadataReferences = metadataReferences,
            Analyzers = analyzers,
            CompilationOptions = InspectionProjectionFactory.CreateCompilationOptionsInfo(project.CompilationOptions, project.ParseOptions),
        };

        var result = PluginExecutionResult.Success(projectDetails);
        return ValueTask.FromResult(result);
    }

    private static BoundedCollection<DocumentReference> CreateDocumentReferences(
        IReadOnlyList<Document> projectDocuments,
        IWorkspacePathService workspacePathService,
        IWorkspaceResolver workspaceResolver,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var candidates = new List<(Document Document, string Path)>();
        foreach (var document in projectDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (workspacePathService.TryNormalizePath(document.FilePath ?? document.Name, out var normalizedPath))
            {
                candidates.Add((document, normalizedPath));
            }
        }

        candidates.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));

        var documents = new List<DocumentReference>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (documents.Count == maxResults)
            {
                return BoundedCollection.CreatePrebounded(documents, hasMore: true);
            }

            var documentReference = workspaceResolver.CreateDocumentReference(candidate.Document);
            if (documentReference is not null)
            {
                documents.Add(documentReference);
            }
        }

        return BoundedCollection.CreatePrebounded(documents, hasMore: false);
    }

    private static BoundedCollection<ProjectReferenceInfo> CreateProjectReferences(
        Project project,
        Solution currentSolution,
        IWorkspacePathService workspacePathService,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var candidates = new List<(Project Project, string SortKey)>();
        foreach (var projectReference in project.ProjectReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var referencedProject = currentSolution.GetProject(projectReference.ProjectId);
            if (referencedProject is null)
            {
                continue;
            }

            if (workspacePathService.TryNormalizePath(referencedProject.FilePath ?? referencedProject.Name, out var sortKey))
            {
                candidates.Add((referencedProject, sortKey));
            }
        }

        candidates.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.SortKey, right.SortKey));

        var projectReferences = new List<ProjectReferenceInfo>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (projectReferences.Count == maxResults)
            {
                return BoundedCollection.CreatePrebounded(projectReferences, candidates.Count);
            }

            projectReferences.Add(InspectionProjectionFactory.CreateProjectReferenceInfo(candidate.Project, candidate.SortKey));
        }

        return BoundedCollection.CreatePrebounded(projectReferences, candidates.Count);
    }

    private static BoundedCollection<MetadataReferenceInfo> CreateMetadataReferences(
        Project project,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var candidates = new List<(MetadataReference Reference, string SortKey)>();
        foreach (var metadataReference in project.MetadataReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sortKey = (metadataReference as PortableExecutableReference)?.FilePath
                ?? metadataReference.Display;

            sortKey ??= metadataReference.GetType().Name;

            candidates.Add((metadataReference, sortKey));
        }

        candidates.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.SortKey, right.SortKey));

        var metadataReferences = new List<MetadataReferenceInfo>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (metadataReferences.Count == maxResults)
            {
                return BoundedCollection.CreatePrebounded(metadataReferences, candidates.Count);
            }

            metadataReferences.Add(InspectionProjectionFactory.CreateMetadataReferenceInfo(candidate.Reference));
        }

        return BoundedCollection.CreatePrebounded(metadataReferences, candidates.Count);
    }

    private static BoundedCollection<AnalyzerInfo> CreateAnalyzers(
        Project project,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var candidates = new List<(AnalyzerReference Reference, string SortKey)>();
        foreach (var analyzerReference in project.AnalyzerReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sortKey = (analyzerReference as AnalyzerFileReference)?.FullPath
                ?? analyzerReference.Display;

            sortKey ??= analyzerReference.GetType().Name;

            candidates.Add((analyzerReference, sortKey));
        }

        candidates.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.SortKey, right.SortKey));

        var analyzers = new List<AnalyzerInfo>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (analyzers.Count == maxResults)
            {
                return BoundedCollection.CreatePrebounded(analyzers, candidates.Count);
            }

            analyzers.Add(InspectionProjectionFactory.CreateAnalyzerInfo(candidate.Reference));
        }

        return BoundedCollection.CreatePrebounded(analyzers, candidates.Count);
    }
}
