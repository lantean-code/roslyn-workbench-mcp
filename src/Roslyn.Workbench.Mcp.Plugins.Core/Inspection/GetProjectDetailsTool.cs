namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-project-details", "Get Project Details", "Returns project metadata, options and selected document details.")]
internal sealed class GetProjectDetailsTool : QueryToolHandler<GetProjectDetailsRequest, ProjectDetailsData>
{
    protected override async ValueTask<PluginExecutionResult<ProjectDetailsData>> ExecuteCoreAsync(GetProjectDetailsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var projectResolution = context.ToolExecutionServices.RequestResolver.ResolveProject<ProjectDetailsData>(request.Project, context);
        if (projectResolution.HasRejection)
        {
            return projectResolution.Rejection;
        }

        var project = projectResolution.Value;
        var targetFrameworks = context.ToolExecutionServices.ProjectStructureService.GetTargetFrameworks(project);
        if (!targetFrameworks.IsSucceeded)
        {
            return ToolExecutionHelpers.RejectProjectStructureFailure<ProjectDetailsData>(targetFrameworks.ErrorMessage);
        }

        var compilation = await project.GetCompilationAsync(cancellationToken);
        var documents = request.IncludeDocuments
            ? project.Documents
                .OrderBy(document => context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name), StringComparer.Ordinal)
                .Select(document => context.WorkspaceResolver.CreateDocumentReference(document))
                .OfType<DocumentReference>()
                .ToArray()
            : null;

        var projectReferences = project.ProjectReferences
            .Select(reference => context.CurrentSolution.GetProject(reference.ProjectId))
            .OfType<Project>()
            .Select(referencedProject => InspectionProjectionFactory.CreateProjectReferenceInfo(referencedProject, context.WorkspaceResolver))
            .OrderBy(static reference => reference.Path, StringComparer.Ordinal)
            .ToArray();
        var metadataReferences = project.MetadataReferences
            .Select(InspectionProjectionFactory.CreateMetadataReferenceInfo)
            .OrderBy(static reference => reference.Path ?? reference.Display, StringComparer.Ordinal)
            .ToArray();
        var analyzers = project.AnalyzerReferences
            .Select(InspectionProjectionFactory.CreateAnalyzerInfo)
            .OrderBy(static analyzer => analyzer.Path ?? analyzer.DisplayName, StringComparer.Ordinal)
            .ToArray();

        return PluginExecutionResult<ProjectDetailsData>.Success(new ProjectDetailsData
        {
            Project = InspectionProjectionFactory.CreateProjectInfo(project, context.WorkspaceResolver, targetFrameworks.TargetFrameworks),
            Documents = documents is null
                ? null
                : ToolExecutionHelpers.CreateBoundedCollection(
                    documents,
                    ToolExecutionHelpers.GetMaxResults(request.DocumentsLimit, GetProjectDetailsRequest._defaultDocumentsMaxResults)),
            ProjectReferences = ToolExecutionHelpers.CreateBoundedCollection(
                projectReferences,
                ToolExecutionHelpers.GetMaxResults(request.ProjectReferencesLimit, GetProjectDetailsRequest._defaultProjectReferencesMaxResults)),
            MetadataReferences = ToolExecutionHelpers.CreateBoundedCollection(
                metadataReferences,
                ToolExecutionHelpers.GetMaxResults(request.MetadataReferencesLimit, GetProjectDetailsRequest._defaultMetadataReferencesMaxResults)),
            Analyzers = ToolExecutionHelpers.CreateBoundedCollection(
                analyzers,
                ToolExecutionHelpers.GetMaxResults(request.AnalyzersLimit, GetProjectDetailsRequest._defaultAnalyzersMaxResults)),
            CompilationOptions = InspectionProjectionFactory.CreateCompilationOptionsInfo(compilation?.Options ?? project.CompilationOptions),
        });
    }
}
