namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-partial-declarations", "Get Partial Declarations", "Returns the declarations for a partial type or method.")]
internal sealed class GetPartialDeclarationsTool : QueryToolHandler<GetPartialDeclarationsRequest, PartialDeclarationsData>
{
    protected override async ValueTask<PluginExecutionResult<PartialDeclarationsData>> ExecuteCoreAsync(GetPartialDeclarationsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<PartialDeclarationsData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var orderedLocations = symbol.DeclaringSyntaxReferences
            .Select(reference => context.WorkspaceResolver.CreateResolvedLocation(reference.SyntaxTree.GetLocation(reference.Span)))
            .OfType<ResolvedLocation>()
            .OrderBy(static location => location.Document?.Path, StringComparer.Ordinal)
            .ThenBy(static location => location.Span?.Start);

        var declarations = new List<ResolvedLocation>();
        var hasMore = false;
        foreach (var resolvedLocation in orderedLocations)
        {
            if (declarations.Count == request.EffectiveDeclarationsLimit)
            {
                hasMore = true;
                break;
            }

            declarations.Add(resolvedLocation);
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);
        var data = new PartialDeclarationsData
        {
            Symbol = symbolReference,
            Declarations = BoundedCollection<ResolvedLocation>.CreatePrebounded(declarations, hasMore),
        };

        return PluginExecutionResult<PartialDeclarationsData>.Success(data);
    }
}
