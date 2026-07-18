namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-document-outline", "Get Document Outline", "Returns a semantic outline for one document.")]
internal sealed class GetDocumentOutlineTool : QueryToolHandler<GetDocumentOutlineRequest, DocumentOutlineData>
{
    protected override async ValueTask<PluginExecutionResult<DocumentOutlineData>> ExecuteCoreAsync(GetDocumentOutlineRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var documentResolution = context.ToolExecutionServices.RequestResolver.ResolveDocument<DocumentOutlineData>(request.Document, context);
        if (documentResolution.HasRejection)
        {
            return documentResolution.Rejection;
        }

        var document = documentResolution.Value;
        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var data = new DocumentOutlineData
        {
            Document = context.WorkspaceResolver.CreateDocumentReference(document),
            Root = syntaxRoot is null || semanticModel is null
                ? null
                : new OutlineNode
                {
                    Name = document.Name,
                    Kind = "Document",
                    Children = DocumentOutlineProjectionFactory.BuildOutlineChildren(syntaxRoot, semanticModel, context.WorkspaceResolver, request.IncludeMembers, cancellationToken),
                },
        };

        return PluginExecutionResult<DocumentOutlineData>.Success(data);
    }
}
