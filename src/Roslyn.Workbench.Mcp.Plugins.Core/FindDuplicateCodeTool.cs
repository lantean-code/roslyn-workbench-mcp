using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class FindDuplicateCodeTool : QueryToolHandler<FindDuplicateCodeRequest, DuplicateCodeData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "find-duplicate-code",
        Title = "Find Duplicate Code",
        Description = "Returns duplicate executable blocks that normalize to the same statement sequence.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new FindDuplicateCodeTool());
    }

    protected override async ValueTask<PluginExecutionResult<DuplicateCodeData>> ExecuteCoreAsync(FindDuplicateCodeRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.MinimumStatements < 1)
        {
            return ToolExecutionHelpers.Rejected<DuplicateCodeData>("InvalidRequest", "MinimumStatements must be at least 1.");
        }

        var documents = ToolExecutionHelpers.ResolveDocuments<DuplicateCodeData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var groups = await CodeStructureAnalysisHelpers.FindDuplicateGroupsAsync(
            documents.Value,
            context,
            request.MinimumStatements,
            cancellationToken).ConfigureAwait(false);

        return ToolExecutionHelpers.CreateBoundedCollectionResult(
            context,
            groups,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            static (items, hasMore) => new DuplicateCodeData
            {
                Groups = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }
}
