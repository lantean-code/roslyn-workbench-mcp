using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class GoToDefinitionTool : QueryToolHandler<GoToDefinitionRequest, DefinitionData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "go-to-definition",
        Title = "Go To Definition",
        Description = "Finds source or metadata definitions for a resolved symbol.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GoToDefinitionTool());
    }

    protected override async ValueTask<PluginExecutionResult<DefinitionData>> ExecuteCoreAsync(GoToDefinitionRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var symbolResolution = await ToolExecutionHelpers.ResolveSymbolAsync<DefinitionData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
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
                    Location = context.Resolver.CreateResolvedLocation(location),
                })
                .Where(static definition => definition.Location is not null)
                .OrderBy(static definition => definition.Location!.Document!.Path, StringComparer.Ordinal)
                .ThenBy(static definition => definition.Location!.Span!.Start)
                .ToArray()
            : [InspectionProjectionFactory.CreateDefinitionLocation(sourceDefinition, context.Resolver)];
        var data = new DefinitionData
        {
            Symbol = context.Resolver.CreateSymbolReference(symbol),
            Definitions = definitions,
        };

        return ToolExecutionHelpers.EnsureWithinSize(context, data);
    }
}
