using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class GetPartialDeclarationsTool : QueryToolHandler<GetPartialDeclarationsRequest, PartialDeclarationsData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-partial-declarations",
        Title = "Get Partial Declarations",
        Description = "Returns the declarations for a partial type or method.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetPartialDeclarationsTool());
    }

    protected override async ValueTask<PluginExecutionResult<PartialDeclarationsData>> ExecuteCoreAsync(GetPartialDeclarationsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var symbolResolution = await ToolExecutionHelpers.ResolveSymbolAsync<PartialDeclarationsData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var declarations = symbol.DeclaringSyntaxReferences
            .Select(reference => context.Resolver.CreateResolvedLocation(reference.SyntaxTree.GetLocation(reference.Span)))
            .Where(static item => item is not null)
            .Select(static item => item!)
            .OrderBy(static item => item.Document!.Path, StringComparer.Ordinal)
            .ThenBy(static item => item.Span!.Start)
            .ToArray();

        return ToolExecutionHelpers.CreateBoundedCollectionResult(
            context,
            declarations,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            (items, hasMore) => new PartialDeclarationsData
            {
                Symbol = context.Resolver.CreateSymbolReference(symbol),
                Declarations = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }
}
