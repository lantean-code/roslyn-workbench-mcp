namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns a bounded semantic outline for one document.
/// </summary>
[RoslynTool("get-document-outline", "Get Document Outline", "Returns a semantic declaration tree for one document, with names, kinds, locations and nested children. Use this to navigate the structure of a known file; use search-symbols to discover declarations across documents, or get-symbol-info for detailed symbol metadata. Members are included by default. Defaults to 200 projected nodes below the document root and 16 hierarchy levels. Check truncated for content omitted by the node or depth limit; root may be null when syntax or semantic information is unavailable.")]
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
