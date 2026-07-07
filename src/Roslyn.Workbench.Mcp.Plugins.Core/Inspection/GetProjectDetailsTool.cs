using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class GetProjectDetailsTool : QueryToolHandler<GetProjectDetailsRequest, QueryResponse<ProjectDetailsData>>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-project-details",
        Title = "Get Project Details",
        Description = "Returns project metadata, options and selected document details.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetProjectDetailsTool());
    }

    protected override async ValueTask<PluginExecutionResult<QueryResponse<ProjectDetailsData>>> ExecuteCoreAsync(GetProjectDetailsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var projectResolution = context.ToolExecutionServices.RequestResolver.ResolveProject<QueryResponse<ProjectDetailsData>>(request.Project, context);
        if (projectResolution.HasRejection)
        {
            return projectResolution.Rejection;
        }

        var project = projectResolution.Value;
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        var targetFrameworks = context.ToolExecutionServices.ProjectStructureService.GetTargetFrameworks(project);
        var documents = request.IncludeDocuments
            ? project.Documents
                .Where(static document => !string.IsNullOrWhiteSpace(document.FilePath))
                .OrderBy(document => context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath!), StringComparer.Ordinal)
                .Select(document => context.WorkspaceResolver.CreateDocumentReference(document)!)
                .ToArray()
            : null;

        if (documents is null)
        {
            var unboundedData = new ProjectDetailsData
            {
                Project = InspectionProjectionFactory.CreateProjectInfo(project, context.WorkspaceResolver, targetFrameworks),
                Documents = null,
                ProjectReferences = project.ProjectReferences
                    .Select(reference => context.CurrentSolution.GetProject(reference.ProjectId))
                    .Where(static referencedProject => referencedProject is not null)
                    .Select(referencedProject => InspectionProjectionFactory.CreateProjectReferenceInfo(referencedProject!, context.WorkspaceResolver))
                    .OrderBy(static reference => reference.Path, StringComparer.Ordinal)
                    .ToArray(),
                MetadataReferences = project.MetadataReferences
                    .Select(InspectionProjectionFactory.CreateMetadataReferenceInfo)
                    .OrderBy(static reference => reference.Path ?? reference.Display, StringComparer.Ordinal)
                    .ToArray(),
                Analyzers = project.AnalyzerReferences
                    .Select(InspectionProjectionFactory.CreateAnalyzerInfo)
                    .OrderBy(static analyzer => analyzer.Path ?? analyzer.DisplayName, StringComparer.Ordinal)
                    .ToArray(),
                CompilationOptions = InspectionProjectionFactory.CreateCompilationOptionsInfo(compilation?.Options ?? project.CompilationOptions),
            };

            return context.ToolExecutionServices.ResultShaper.CreateSingletonResponse(context, unboundedData);
        }

        var projectReferences = project.ProjectReferences
            .Select(reference => context.CurrentSolution.GetProject(reference.ProjectId))
            .Where(static referencedProject => referencedProject is not null)
            .Select(referencedProject => InspectionProjectionFactory.CreateProjectReferenceInfo(referencedProject!, context.WorkspaceResolver))
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

        return CreateBoundedResponse(
            context,
            documents,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            (items, hasMore) => new ProjectDetailsData
            {
                Project = InspectionProjectionFactory.CreateProjectInfo(project, context.WorkspaceResolver, targetFrameworks),
                Documents = items,
                ProjectReferences = projectReferences,
                MetadataReferences = metadataReferences,
                Analyzers = analyzers,
                CompilationOptions = InspectionProjectionFactory.CreateCompilationOptionsInfo(compilation?.Options ?? project.CompilationOptions),
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }

    private static PluginExecutionResult<QueryResponse<ProjectDetailsData>> CreateBoundedResponse(
        IQueryContext context,
        IReadOnlyList<DocumentReference> orderedDocuments,
        int maxResults,
        Func<IReadOnlyList<DocumentReference>, bool, ProjectDetailsData> createData)
    {
        var limitedCount = Math.Min(maxResults, orderedDocuments.Count);

        for (var count = limitedCount; count >= 0; count--)
        {
            var documents = count == orderedDocuments.Count
                ? orderedDocuments
                : orderedDocuments.Take(count).ToArray();
            var result = context.ToolExecutionServices.ResultShaper.CreateSingletonResponse(
                context,
                createData(documents, count < orderedDocuments.Count));

            if (result.Outcome == ToolOutcome.Succeeded)
            {
                return result;
            }
        }

        return context.ToolExecutionServices.ResultShaper.Rejected<QueryResponse<ProjectDetailsData>>(
            "ResponseLimitExceeded",
            "The response exceeded the configured response size limit.",
            RequiredAction.NarrowRequest);
    }
}
