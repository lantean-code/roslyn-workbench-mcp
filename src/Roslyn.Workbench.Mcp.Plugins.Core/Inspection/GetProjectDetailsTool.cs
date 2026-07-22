namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-project-details", "Get Project Details", "Returns project metadata, options and selected document details.")]
internal sealed class GetProjectDetailsTool : QueryToolHandler<GetProjectDetailsRequest, ProjectDetailsData>
{
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
            var rejection = ToolExecutionHelpers.RejectProjectStructureFailure<ProjectDetailsData>(targetFrameworks.ErrorMessage);
            return ValueTask.FromResult(rejection);
        }

        BoundedCollection<DocumentReference>? documents = null;
        if (request.IncludeDocuments)
        {
            documents = CreateDocumentReferences(
                project,
                context.WorkspaceResolver,
                request.EffectiveDocumentsLimit,
                cancellationToken);
        }

        var projectReferences = CreateProjectReferences(
            project,
            context.CurrentSolution,
            context.WorkspaceResolver,
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
            Project = InspectionProjectionFactory.CreateProjectInfo(project, context.WorkspaceResolver, targetFrameworks.TargetFrameworks),
            Documents = documents,
            ProjectReferences = projectReferences,
            MetadataReferences = metadataReferences,
            Analyzers = analyzers,
            CompilationOptions = InspectionProjectionFactory.CreateCompilationOptionsInfo(project.CompilationOptions),
        };

        var result = PluginExecutionResult<ProjectDetailsData>.Success(projectDetails);
        return ValueTask.FromResult(result);
    }

    private static BoundedCollection<DocumentReference> CreateDocumentReferences(
        Project project,
        IWorkspaceResolver workspaceResolver,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var candidates = new List<(Document Document, string SortKey)>();
        foreach (var document in project.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sortKey = workspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name);
            candidates.Add((document, sortKey));
        }

        candidates.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.SortKey, right.SortKey));

        var documents = new List<DocumentReference>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var documentReference = workspaceResolver.CreateDocumentReference(candidate.Document);
            if (documentReference is null)
            {
                continue;
            }

            if (documents.Count == maxResults)
            {
                return ToolExecutionHelpers.CreatePreboundedCollection(documents, hasMore: true);
            }

            documents.Add(documentReference);
        }

        return ToolExecutionHelpers.CreatePreboundedCollection(documents, hasMore: false);
    }

    private static BoundedCollection<ProjectReferenceInfo> CreateProjectReferences(
        Project project,
        Solution currentSolution,
        IWorkspaceResolver workspaceResolver,
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

            var sortKey = workspaceResolver.NormalizeProjectPath(referencedProject.FilePath ?? referencedProject.Name);
            candidates.Add((referencedProject, sortKey));
        }

        candidates.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.SortKey, right.SortKey));

        var projectReferences = new List<ProjectReferenceInfo>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (projectReferences.Count == maxResults)
            {
                return ToolExecutionHelpers.CreatePreboundedCollection(projectReferences, hasMore: true);
            }

            projectReferences.Add(InspectionProjectionFactory.CreateProjectReferenceInfo(candidate.Project, workspaceResolver));
        }

        return ToolExecutionHelpers.CreatePreboundedCollection(projectReferences, hasMore: false);
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
                return ToolExecutionHelpers.CreatePreboundedCollection(metadataReferences, hasMore: true);
            }

            metadataReferences.Add(InspectionProjectionFactory.CreateMetadataReferenceInfo(candidate.Reference));
        }

        return ToolExecutionHelpers.CreatePreboundedCollection(metadataReferences, hasMore: false);
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
                return ToolExecutionHelpers.CreatePreboundedCollection(analyzers, hasMore: true);
            }

            analyzers.Add(InspectionProjectionFactory.CreateAnalyzerInfo(candidate.Reference));
        }

        return ToolExecutionHelpers.CreatePreboundedCollection(analyzers, hasMore: false);
    }
}
