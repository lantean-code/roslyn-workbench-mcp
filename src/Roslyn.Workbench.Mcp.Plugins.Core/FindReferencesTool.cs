using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Contracts.Inspection;

using ContractReferenceLocation = Roslyn.Workbench.Mcp.Contracts.Inspection.ReferenceLocation;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class FindReferencesTool : QueryToolHandler<FindReferencesRequest, ReferenceSearchData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "find-references",
        Title = "Find References",
        Description = "Finds source references, optionally including declarations and access classification.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new FindReferencesTool());
    }

    protected override async ValueTask<PluginExecutionResult<ReferenceSearchData>> ExecuteCoreAsync(FindReferencesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var symbolResolution = await ToolExecutionHelpers.ResolveSymbolAsync<ReferenceSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var documents = ToolExecutionHelpers.ResolveDocuments<ReferenceSearchData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var referencedSymbols = await SymbolFinder.FindReferencesAsync(
                symbol,
                context.CurrentSolution,
                documents.Value.ToImmutableHashSet(),
                cancellationToken).ConfigureAwait(false);
        var references = new List<ContractReferenceLocation>();

        foreach (var referencedSymbol in referencedSymbols)
        {
            if (request.IncludeDefinitions)
            {
                references.AddRange(referencedSymbol.Definition.Locations
                    .Where(static location => location.IsInSource)
                    .Select(location => new ContractReferenceLocation
                    {
                        Location = context.Resolver.CreateResolvedLocation(location),
                        ContainingSymbol = context.Resolver.CreateSymbolReference(referencedSymbol.Definition),
                        IsDefinition = true,
                    })
                    .Where(static location => location.Location is not null));
            }

            var referenceEntries = referencedSymbol.Locations
                .Where(static reference => reference.Location.IsInSource)
                .ToArray();

            foreach (var reference in referenceEntries)
            {
                var containingSymbol = reference.Document is null
                    ? null
                    : await ToolExecutionHelpers.TryCreateContainingSymbolAsync(reference.Document, reference.Location.SourceSpan.Start, context, cancellationToken).ConfigureAwait(false);
                var contextLine = request.IncludeContext
                    ? await ToolExecutionHelpers.ReadContextAsync(reference.Document, reference.Location.SourceSpan, cancellationToken).ConfigureAwait(false)
                    : null;

                var location = context.Resolver.CreateResolvedLocation(reference.Location);
                if (location is null)
                {
                    continue;
                }

                references.Add(new ContractReferenceLocation
                {
                    Location = location,
                    ContainingSymbol = containingSymbol is null ? null : context.Resolver.CreateSymbolReference(containingSymbol),
                    IsWrite = await IsWriteReferenceAsync(reference, cancellationToken).ConfigureAwait(false),
                    Context = contextLine,
                });
            }
        }

        var orderedReferences = references
            .OrderBy(static reference => reference.Location!.Document!.Path, StringComparer.Ordinal)
            .ThenBy(static reference => reference.Location!.Span!.Start)
            .ToArray();
        var symbolReference = context.Resolver.CreateSymbolReference(symbol);

        return ToolExecutionHelpers.CreateBoundedCollectionResult(
            context,
            orderedReferences,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            (items, hasMore) => new ReferenceSearchData
            {
                Symbol = symbolReference,
                References = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }

    private static async ValueTask<bool> IsWriteReferenceAsync(Microsoft.CodeAnalysis.FindSymbols.ReferenceLocation reference, CancellationToken cancellationToken)
    {
        if (reference.Document is null)
        {
            return false;
        }

        var syntaxRoot = await reference.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (syntaxRoot is null)
        {
            return false;
        }

        var node = syntaxRoot.FindNode(reference.Location.SourceSpan, getInnermostNodeForTie: true);
        for (SyntaxNode? current = node; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AssignmentExpressionSyntax assignmentExpressionSyntax
                    when assignmentExpressionSyntax.Left.Span.Contains(reference.Location.SourceSpan):
                    return true;

                case PrefixUnaryExpressionSyntax prefixUnaryExpressionSyntax
                    when prefixUnaryExpressionSyntax.IsKind(SyntaxKind.PreIncrementExpression)
                        || prefixUnaryExpressionSyntax.IsKind(SyntaxKind.PreDecrementExpression):
                    return true;

                case PostfixUnaryExpressionSyntax postfixUnaryExpressionSyntax
                    when postfixUnaryExpressionSyntax.IsKind(SyntaxKind.PostIncrementExpression)
                        || postfixUnaryExpressionSyntax.IsKind(SyntaxKind.PostDecrementExpression):
                    return true;

                case ArgumentSyntax argumentSyntax
                    when argumentSyntax.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)
                        || argumentSyntax.RefKindKeyword.IsKind(SyntaxKind.RefKeyword):
                    return true;
            }
        }

        return false;
    }
}
