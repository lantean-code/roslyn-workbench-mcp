namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns the declarations for a partial type or method.
/// </summary>
[RoslynTool("get-partial-declarations", "Get Partial Declarations", "Returns the declarations for a partial type or method.")]
internal sealed class GetPartialDeclarationsTool : QueryToolHandler<GetPartialDeclarationsRequest, PartialDeclarationsData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<PartialDeclarationsData>> ExecuteCoreAsync(GetPartialDeclarationsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<PartialDeclarationsData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var resolvedLocations = new List<ResolvedLocation>();
        foreach (var declaration in symbol.DeclaringSyntaxReferences)
        {
            var location = declaration.SyntaxTree.GetLocation(declaration.Span);
            var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
            if (resolvedLocation is not null)
            {
                resolvedLocations.Add(resolvedLocation);
            }
        }

        var orderedLocations = resolvedLocations
            .OrderBy(static location => location.Document?.Path, StringComparer.Ordinal)
            .ThenBy(static location => location.Span?.Start);

        var declarations = new List<ResolvedLocation>();
        foreach (var resolvedLocation in orderedLocations)
        {
            if (declarations.Count == request.EffectiveDeclarationsLimit)
            {
                break;
            }

            declarations.Add(resolvedLocation);
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);
        var data = new PartialDeclarationsData
        {
            Symbol = symbolReference,
            Declarations = BoundedCollection.CreatePrebounded(declarations, resolvedLocations.Count),
        };

        return PluginExecutionResult.Success(data);
    }
}
