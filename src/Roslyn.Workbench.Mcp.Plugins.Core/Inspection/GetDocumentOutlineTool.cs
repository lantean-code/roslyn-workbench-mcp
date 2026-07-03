using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class GetDocumentOutlineTool : QueryToolHandler<GetDocumentOutlineRequest, DocumentOutlineData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-document-outline",
        Title = "Get Document Outline",
        Description = "Returns a semantic outline for one document.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetDocumentOutlineTool());
    }

    protected override async ValueTask<PluginExecutionResult<DocumentOutlineData>> ExecuteCoreAsync(GetDocumentOutlineRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var documentResolution = ToolExecutionHelpers.ResolveDocument<DocumentOutlineData>(request.Document, context);
        if (documentResolution.HasRejection)
        {
            return documentResolution.Rejection;
        }

        var document = documentResolution.Value;
        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var data = new DocumentOutlineData
        {
            Document = context.Resolver.CreateDocumentReference(document),
            Root = syntaxRoot is null || semanticModel is null
                ? null
                : new OutlineNode
                {
                    Name = document.Name,
                    Kind = "Document",
                    Children = InspectionProjectionFactory.BuildOutlineChildren(syntaxRoot, semanticModel, context.Resolver, request.IncludeMembers, cancellationToken),
                },
        };

        return ToolExecutionHelpers.EnsureWithinSize(context, data);
    }
}
