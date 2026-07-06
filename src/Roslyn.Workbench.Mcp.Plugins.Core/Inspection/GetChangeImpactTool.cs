using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Contracts.Inspection;

using ContractReferenceLocation = Roslyn.Workbench.Mcp.Contracts.Inspection.ReferenceLocation;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class GetChangeImpactTool : QueryToolHandler<GetChangeImpactRequest, ChangeImpactData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-change-impact",
        Title = "Get Change Impact",
        Description = "Returns a bounded impact summary and supporting source locations for a symbol change.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetChangeImpactTool());
    }

    protected override async ValueTask<PluginExecutionResult<ChangeImpactData>> ExecuteCoreAsync(GetChangeImpactRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<ChangeImpactData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<ChangeImpactData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var projects = context.ToolExecutionServices.RequestResolver.ResolveProjects<ChangeImpactData>(request.Scope, context);
        if (projects.HasRejection)
        {
            return projects.Rejection;
        }

        var referencedSymbols = await SymbolFinder.FindReferencesAsync(symbol, context.CurrentSolution, documents.Value.ToImmutableHashSet(), cancellationToken).ConfigureAwait(false);
        var locations = new List<ContractReferenceLocation>();

        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var reference in referencedSymbol.Locations.Where(static item => item.Location.IsInSource))
            {
                var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(reference.Location);
                if (resolvedLocation is null)
                {
                    continue;
                }

                var containingSymbol = reference.Document is null
                    ? null
                    : await GetEnclosingSymbolAsync(reference.Document, reference.Location.SourceSpan.Start, cancellationToken).ConfigureAwait(false);
                var contextLine = await context.ToolExecutionServices.InspectionContextService.ReadContextAsync(reference.Document, reference.Location.SourceSpan, cancellationToken).ConfigureAwait(false);

                locations.Add(new ContractReferenceLocation
                {
                    Location = resolvedLocation,
                    ContainingSymbol = containingSymbol is null ? null : context.WorkspaceResolver.CreateSymbolReference(containingSymbol),
                    Context = contextLine,
                });
            }
        }

        var referenceCount = locations.Count;
        var callerCount = symbol is IMethodSymbol
            ? (await SymbolFinder.FindCallersAsync(symbol, context.CurrentSolution, documents.Value.ToImmutableHashSet(), cancellationToken).ConfigureAwait(false)).Count()
            : 0;
        var overrideCount = symbol is IMethodSymbol or IPropertySymbol or IEventSymbol
            ? (await SymbolFinder.FindOverridesAsync(symbol, context.CurrentSolution, projects.Value.ToImmutableHashSet(), cancellationToken).ConfigureAwait(false)).Distinct(SymbolEqualityComparer.Default).Count()
            : 0;
        var implementationCount = symbol switch
        {
            INamedTypeSymbol namedTypeSymbol when namedTypeSymbol.TypeKind == TypeKind.Interface
                => (await SymbolFinder.FindImplementationsAsync(namedTypeSymbol, context.CurrentSolution, projects.Value.ToImmutableHashSet(), cancellationToken).ConfigureAwait(false)).Distinct(SymbolEqualityComparer.Default).Count(),
            _ => 0,
        };

        var orderedLocations = locations
            .OrderBy(static location => location.Location!.Document!.Path, StringComparer.Ordinal)
            .ThenBy(static location => location.Location!.Span!.Start)
            .ToArray();
        var impact = new ImpactSummary
        {
            ReferenceCount = referenceCount,
            CallerCount = callerCount,
            OverrideCount = overrideCount,
            ImplementationCount = implementationCount,
            PublicSurfaceCount = IsPublicSurface(symbol) ? 1 : 0,
        };

        return context.ToolExecutionServices.ResultShaper.CreateBoundedCollectionResult(
            context,
            orderedLocations,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            (items, hasMore) => new ChangeImpactData
            {
                Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
                Impact = impact,
                Locations = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }

    private static bool IsPublicSurface(ISymbol symbol)
    {
        return symbol.DeclaredAccessibility is Accessibility.Public
            or Accessibility.Protected
            or Accessibility.ProtectedOrInternal
            or Accessibility.ProtectedAndInternal;
    }

    private static async ValueTask<ISymbol?> GetEnclosingSymbolAsync(Document document, int position, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        return semanticModel?.GetEnclosingSymbol(position, cancellationToken);
    }
}
