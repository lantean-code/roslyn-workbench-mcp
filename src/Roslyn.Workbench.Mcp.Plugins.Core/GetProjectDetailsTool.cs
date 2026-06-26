using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class GetProjectDetailsTool : QueryToolHandler<GetProjectDetailsRequest, ProjectDetailsData>
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

    protected override async ValueTask<PluginExecutionResult<ProjectDetailsData>> ExecuteCoreAsync(GetProjectDetailsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var projectResolution = ToolExecutionHelpers.ResolveProject<ProjectDetailsData>(request.Project, context);
        if (projectResolution.HasRejection)
        {
            return projectResolution.Rejection;
        }

        var project = projectResolution.Value;
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        var documents = request.IncludeDocuments
            ? project.Documents
                .Where(static document => !string.IsNullOrWhiteSpace(document.FilePath))
                .OrderBy(document => context.Resolver.NormalizeDocumentPath(document.FilePath!), StringComparer.Ordinal)
                .Select(document => context.Resolver.CreateDocumentReference(document)!)
                .ToArray()
            : null;

        if (documents is null)
        {
            var unboundedData = new ProjectDetailsData
            {
                Project = InspectionProjectionFactory.CreateProjectInfo(project, context.Resolver),
                Documents = null,
                ProjectReferences = project.ProjectReferences
                    .Select(reference => context.CurrentSolution.GetProject(reference.ProjectId))
                    .Where(static referencedProject => referencedProject is not null)
                    .Select(referencedProject => InspectionProjectionFactory.CreateProjectReferenceInfo(referencedProject!, context.Resolver))
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

            return ToolExecutionHelpers.EnsureWithinSize(context, unboundedData);
        }

        var projectReferences = project.ProjectReferences
            .Select(reference => context.CurrentSolution.GetProject(reference.ProjectId))
            .Where(static referencedProject => referencedProject is not null)
            .Select(referencedProject => InspectionProjectionFactory.CreateProjectReferenceInfo(referencedProject!, context.Resolver))
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

        return ToolExecutionHelpers.CreateBoundedCollectionResult(
            context,
            documents,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            (items, hasMore) => new ProjectDetailsData
            {
                Project = InspectionProjectionFactory.CreateProjectInfo(project, context.Resolver),
                Documents = items,
                ProjectReferences = projectReferences,
                MetadataReferences = metadataReferences,
                Analyzers = analyzers,
                CompilationOptions = InspectionProjectionFactory.CreateCompilationOptionsInfo(compilation?.Options ?? project.CompilationOptions),
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }
}
