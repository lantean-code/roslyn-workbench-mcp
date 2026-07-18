namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-partial-declarations", "Get Partial Declarations", "Returns the declarations for a partial type or method.")]
internal sealed class GetPartialDeclarationsTool : QueryToolHandler<GetPartialDeclarationsRequest, PartialDeclarationsData>
{
    protected override async ValueTask<PluginExecutionResult<PartialDeclarationsData>> ExecuteCoreAsync(GetPartialDeclarationsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<PartialDeclarationsData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var declarations = symbol.DeclaringSyntaxReferences
            .Select(reference => context.WorkspaceResolver.CreateResolvedLocation(reference.SyntaxTree.GetLocation(reference.Span)))
            .OfType<ResolvedLocation>()
            .OrderBy(static item => item.Document?.Path, StringComparer.Ordinal)
            .ThenBy(static item => item.Span?.Start)
            .ToArray();

        return PluginExecutionResult<PartialDeclarationsData>.Success(new PartialDeclarationsData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Declarations = ToolExecutionHelpers.CreateBoundedCollection(
                declarations,
                ToolExecutionHelpers.GetMaxResults(context, request.DeclarationsLimit)),
        });
    }
}
