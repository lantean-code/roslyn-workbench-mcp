using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

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
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        var targetFrameworks = context.ToolExecutionServices.ProjectStructureService.GetTargetFrameworks(project);
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
            Project = InspectionProjectionFactory.CreateProjectInfo(project, context.WorkspaceResolver, targetFrameworks),
            Documents = documents is null
                ? null
                : ToolExecutionHelpers.CreateBoundedCollection(
                    documents,
                    ToolExecutionHelpers.GetMaxResults(context, request.DocumentsLimit)),
            ProjectReferences = ToolExecutionHelpers.CreateBoundedCollection(
                projectReferences,
                ToolExecutionHelpers.GetMaxResults(context, request.ProjectReferencesLimit)),
            MetadataReferences = ToolExecutionHelpers.CreateBoundedCollection(
                metadataReferences,
                ToolExecutionHelpers.GetMaxResults(context, request.MetadataReferencesLimit)),
            Analyzers = ToolExecutionHelpers.CreateBoundedCollection(
                analyzers,
                ToolExecutionHelpers.GetMaxResults(context, request.AnalyzersLimit)),
            CompilationOptions = InspectionProjectionFactory.CreateCompilationOptionsInfo(compilation?.Options ?? project.CompilationOptions),
        });
    }
}
