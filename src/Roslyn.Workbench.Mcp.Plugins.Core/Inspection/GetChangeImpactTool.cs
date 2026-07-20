using System.Collections.Immutable;

using ContractReferenceLocation = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.ReferenceLocation;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-change-impact", "Get Change Impact", "Returns a bounded impact summary and supporting source locations for a symbol change.")]
internal sealed class GetChangeImpactTool : QueryToolHandler<GetChangeImpactRequest, ChangeImpactData>
{
    protected override async ValueTask<PluginExecutionResult<ChangeImpactData>> ExecuteCoreAsync(GetChangeImpactRequest request, IQueryContext context, CancellationToken cancellationToken)
    {

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<ChangeImpactData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
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

        var documentSet = documents.Value.ToImmutableHashSet();
        var projectSet = projects.Value.ToImmutableHashSet();
        var referencedSymbols = await SymbolFinder.FindReferencesAsync(symbol, context.CurrentSolution, documentSet, cancellationToken);
        var pendingReferences = new List<PendingReference>();

        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var reference in referencedSymbol.Locations)
            {
                if (!reference.Location.IsInSource
                    || context.WorkspaceResolver.CreateResolvedLocation(reference.Location) is not { } resolvedLocation)
                {
                    continue;
                }

                pendingReferences.Add(new PendingReference
                {
                    Location = resolvedLocation,
                    Reference = reference,
                });
            }
        }

        var referenceCount = pendingReferences.Count;
        var callerCount = 0;
        if (symbol is IMethodSymbol)
        {
            var callers = await SymbolFinder.FindCallersAsync(symbol, context.CurrentSolution, documentSet, cancellationToken);
            callerCount = callers.Count();
        }

        var overrideCount = 0;
        if (symbol is IMethodSymbol or IPropertySymbol or IEventSymbol)
        {
            var overrides = await SymbolFinder.FindOverridesAsync(symbol, context.CurrentSolution, projectSet, cancellationToken);
            overrideCount = overrides.Distinct(SymbolEqualityComparer.Default).Count();
        }

        var implementationCount = 0;
        if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Interface } namedTypeSymbol)
        {
            var implementations = await SymbolFinder.FindImplementationsAsync(namedTypeSymbol, context.CurrentSolution, projectSet, cancellationToken);
            implementationCount = implementations.Distinct(SymbolEqualityComparer.Default).Count();
        }

        var maxResults = request.EffectiveLocationsLimit;
        var selectedReferences = pendingReferences
            .OrderBy(static reference => reference.Location.Document?.Path, StringComparer.Ordinal)
            .ThenBy(static reference => reference.Location.Span?.Start)
            .Take(maxResults)
            .ToArray();

        var locations = new List<ContractReferenceLocation>(selectedReferences.Length);
        foreach (var pendingReference in selectedReferences)
        {
            var reference = pendingReference.Reference;
            ISymbol? containingSymbol = null;
            if (reference.Document is not null)
            {
                containingSymbol = await GetEnclosingSymbolAsync(
                    reference.Document,
                    reference.Location.SourceSpan.Start,
                    cancellationToken);
            }

            var contextLine = await context.ToolExecutionServices.InspectionContextService.ReadContextAsync(reference.Document, reference.Location.SourceSpan, cancellationToken);

            locations.Add(new ContractReferenceLocation
            {
                Location = pendingReference.Location,
                ContainingSymbol = containingSymbol is null ? null : context.WorkspaceResolver.CreateSymbolReference(containingSymbol),
                Context = contextLine,
            });
        }

        var impact = new ImpactSummary
        {
            ReferenceCount = referenceCount,
            CallerCount = callerCount,
            OverrideCount = overrideCount,
            ImplementationCount = implementationCount,
            PublicSurfaceCount = IsPublicSurface(symbol) ? 1 : 0,
        };

        var data = new ChangeImpactData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Impact = impact,
            Locations = ToolExecutionHelpers.CreatePreboundedCollection(
                locations,
                pendingReferences.Count > maxResults),
        };

        return PluginExecutionResult<ChangeImpactData>.Success(data);
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
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        return semanticModel?.GetEnclosingSymbol(position, cancellationToken);
    }

    private readonly record struct PendingReference
    {
        public required Microsoft.CodeAnalysis.FindSymbols.ReferenceLocation Reference { get; init; }

        public required ResolvedLocation Location { get; init; }
    }
}
