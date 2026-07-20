namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("go-to-definition", "Go To Definition", "Finds source or metadata definitions for a resolved symbol.")]
internal sealed class GoToDefinitionTool : QueryToolHandler<GoToDefinitionRequest, DefinitionData>
{
    protected override async ValueTask<PluginExecutionResult<DefinitionData>> ExecuteCoreAsync(GoToDefinitionRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<DefinitionData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var sourceDefinition = await SymbolFinder.FindSourceDefinitionAsync(symbol, context.CurrentSolution, cancellationToken) ?? symbol;
        var sourceDefinitions = new List<DefinitionLocation>();
        var hasSourceLocation = false;
        foreach (var location in sourceDefinition.Locations)
        {
            if (!location.IsInSource)
            {
                continue;
            }

            hasSourceLocation = true;
            var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
            if (resolvedLocation is not null)
            {
                sourceDefinitions.Add(new DefinitionLocation
                {
                    Location = resolvedLocation,
                });
            }
        }

        IReadOnlyList<DefinitionLocation> definitions;
        if (!hasSourceLocation)
        {
            definitions = [InspectionProjectionFactory.CreateDefinitionLocation(sourceDefinition, context.WorkspaceResolver)];
        }
        else
        {
            definitions = sourceDefinitions
                .OrderBy(static definition => definition.Location?.Document?.Path, StringComparer.Ordinal)
                .ThenBy(static definition => definition.Location?.Span?.Start)
                .ToArray();
        }

        var data = new DefinitionData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Definitions = definitions,
        };

        return PluginExecutionResult<DefinitionData>.Success(data);
    }
}
