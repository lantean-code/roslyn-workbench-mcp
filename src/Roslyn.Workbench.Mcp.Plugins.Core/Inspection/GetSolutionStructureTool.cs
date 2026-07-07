using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class GetSolutionStructureTool : QueryToolHandler<GetSolutionStructureRequest, SolutionStructureData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-solution-structure",
        Title = "Get Solution Structure",
        Description = "Returns solution folders, projects, target frameworks and direct project relationships.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetSolutionStructureTool());
    }

    protected override async ValueTask<PluginExecutionResult<SolutionStructureData>> ExecuteCoreAsync(GetSolutionStructureRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hierarchy = await context.ToolExecutionServices.ProjectStructureService.GetSolutionHierarchyAsync(context.WorkspaceIdentity.LoadedPath, cancellationToken).ConfigureAwait(false);

        var projects = context.CurrentSolution.Projects
            .OrderBy(project => context.WorkspaceResolver.NormalizeProjectPath(project.FilePath ?? project.Name), StringComparer.Ordinal)
            .Select(project => new ProjectStructureInfo
            {
                ProjectId = project.Id.Id.ToString(),
                Name = project.Name,
                Path = context.WorkspaceResolver.NormalizeProjectPath(project.FilePath ?? project.Name),
                SolutionFolderPath = hierarchy.ProjectFolderPaths.TryGetValue(context.WorkspaceResolver.NormalizeProjectPath(project.FilePath ?? project.Name), out var solutionFolderPath)
                    ? solutionFolderPath
                    : null,
                TargetFrameworks = context.ToolExecutionServices.ProjectStructureService.GetTargetFrameworks(project),
                ProjectReferences = project.ProjectReferences
                    .Select(reference => context.CurrentSolution.GetProject(reference.ProjectId))
                    .Where(static project => project is not null)
                    .Select(project => InspectionProjectionFactory.CreateProjectReferenceInfo(project!, context.WorkspaceResolver))
                    .OrderBy(static reference => reference.Path, StringComparer.Ordinal)
                    .ToArray(),
                Documents = request.IncludeDocuments
                    ? project.Documents
                        .Where(static document => !string.IsNullOrWhiteSpace(document.FilePath))
                        .OrderBy(document => context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath!), StringComparer.Ordinal)
                        .Select(document => context.WorkspaceResolver.CreateDocumentReference(document)!)
                        .ToArray()
                    : null,
            })
            .ToArray();

        return PluginExecutionResult<SolutionStructureData>.Success(new SolutionStructureData
        {
            SolutionPath = context.WorkspaceIdentity.LoadedPath,
            Folders = ToolExecutionHelpers.CreateBoundedCollection(
                hierarchy.Folders,
                ToolExecutionHelpers.GetMaxResults(context, request.FoldersLimit)),
            Projects = ToolExecutionHelpers.CreateBoundedCollection(
                projects,
                ToolExecutionHelpers.GetMaxResults(context, request.ProjectsLimit)),
        });
    }
}
