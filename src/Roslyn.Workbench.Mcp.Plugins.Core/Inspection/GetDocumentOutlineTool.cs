namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns a bounded semantic outline for one document.
/// </summary>
[RoslynTool("get-document-outline", "Get Document Outline", "Returns a bounded semantic outline for one document.")]
internal sealed class GetDocumentOutlineTool : QueryToolHandler<GetDocumentOutlineRequest, DocumentOutlineData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<DocumentOutlineData>> ExecuteCoreAsync(GetDocumentOutlineRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var documentResolution = context.ToolExecutionServices.RequestResolver.ResolveDocument<DocumentOutlineData>(request.Document, context);
        if (documentResolution.HasRejection)
        {
            return documentResolution.Rejection;
        }

        var document = documentResolution.Value;
        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        OutlineNode? root = null;
        var truncated = false;
        if (syntaxRoot is not null && semanticModel is not null)
        {
            root = new OutlineNode
            {
                Name = document.Name,
                Kind = "Document",
                Children = DocumentOutlineProjectionFactory.BuildOutlineChildren(
                    syntaxRoot,
                    semanticModel,
                    context.WorkspaceResolver,
                    request.IncludeMembers,
                    request.EffectiveNodesLimit,
                    request.MaxDepth,
                    out truncated,
                    cancellationToken),
            };
        }

        var data = new DocumentOutlineData
        {
            Document = context.WorkspaceResolver.CreateDocumentReference(document),
            Root = root,
            Truncated = truncated,
        };

        return PluginExecutionResult.Success(data);
    }
}
