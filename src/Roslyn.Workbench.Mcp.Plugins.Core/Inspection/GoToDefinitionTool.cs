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
        var sourceLocations = sourceDefinition.Locations
            .Where(static location => location.IsInSource)
            .OrderBy(static location => location.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(static location => location.SourceSpan.Start)
            .ToArray();

        BoundedCollection<DefinitionLocation> definitions;
        if (sourceLocations.Length == 0)
        {
            var metadataDefinition = InspectionProjectionFactory.CreateDefinitionLocation(sourceDefinition, context.WorkspaceResolver);
            var items = request.EffectiveDefinitionsLimit == 0
                ? Array.Empty<DefinitionLocation>()
                : [metadataDefinition];

            definitions = BoundedCollection.CreatePrebounded(items, totalCount: 1);
        }
        else
        {
            definitions = CreateSourceDefinitions(
                sourceLocations,
                request.EffectiveDefinitionsLimit,
                context.WorkspaceResolver);
        }

        var data = new DefinitionData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Definitions = definitions,
        };

        return PluginExecutionResult.Success(data);
    }

    private static BoundedCollection<DefinitionLocation> CreateSourceDefinitions(
        IReadOnlyList<Location> sourceLocations,
        int maxResults,
        IWorkspaceResolver workspaceResolver)
    {
        var definitions = new List<DefinitionLocation>();
        foreach (var location in sourceLocations)
        {
            var resolvedLocation = workspaceResolver.CreateResolvedLocation(location);
            if (resolvedLocation is null)
            {
                continue;
            }

            if (definitions.Count == maxResults)
            {
                return BoundedCollection.CreatePrebounded(definitions, hasMore: true);
            }

            definitions.Add(new DefinitionLocation
            {
                Location = resolvedLocation,
            });
        }

        return BoundedCollection.CreatePrebounded(definitions, hasMore: false);
    }
}
