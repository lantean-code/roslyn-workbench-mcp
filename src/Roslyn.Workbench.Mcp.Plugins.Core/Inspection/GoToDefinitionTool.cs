namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("go-to-definition", "Go To Definition", "Finds source or metadata definitions for a resolved symbol.")]
internal sealed class GoToDefinitionTool : QueryToolHandler<GoToDefinitionRequest, DefinitionData>
{
    protected override async ValueTask<PluginExecutionResult<DefinitionData>> ExecuteCoreAsync(GoToDefinitionRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<DefinitionData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var sourceDefinition = await SymbolFinder.FindSourceDefinitionAsync(symbol, context.CurrentSolution, cancellationToken).ConfigureAwait(false) ?? symbol;
        var definitions = sourceDefinition.Locations.Any(static location => location.IsInSource)
            ? sourceDefinition.Locations
                .Where(static location => location.IsInSource)
                .Select(location => new DefinitionLocation
                {
                    Location = context.WorkspaceResolver.CreateResolvedLocation(location),
                })
                .Where(static definition => definition.Location is not null)
                .OrderBy(static definition => definition.Location?.Document?.Path, StringComparer.Ordinal)
                .ThenBy(static definition => definition.Location?.Span?.Start)
                .ToArray()
            : [InspectionProjectionFactory.CreateDefinitionLocation(sourceDefinition, context.WorkspaceResolver)];
        var data = new DefinitionData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Definitions = definitions,
        };

        return PluginExecutionResult<DefinitionData>.Success(data);
    }
}
