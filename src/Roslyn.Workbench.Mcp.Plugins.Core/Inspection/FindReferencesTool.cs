using System.Collections.Immutable;

using ContractReferenceLocation = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.ReferenceLocation;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-references", "Find References", "Finds source references, optionally including declarations and access classification.")]
internal sealed class FindReferencesTool : QueryToolHandler<FindReferencesRequest, ReferenceSearchData>
{
    protected override async ValueTask<PluginExecutionResult<ReferenceSearchData>> ExecuteCoreAsync(FindReferencesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<ReferenceSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<ReferenceSearchData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var referencedSymbols = await SymbolFinder.FindReferencesAsync(
                symbol,
                context.CurrentSolution,
                documents.Value.ToImmutableHashSet(),
                cancellationToken);
        var references = new List<ContractReferenceLocation>();

        foreach (var referencedSymbol in referencedSymbols)
        {
            if (request.IncludeDefinitions)
            {
                references.AddRange(referencedSymbol.Definition.Locations
                    .Where(static location => location.IsInSource)
                    .Select(location => new ContractReferenceLocation
                    {
                        Location = context.WorkspaceResolver.CreateResolvedLocation(location),
                        ContainingSymbol = context.WorkspaceResolver.CreateSymbolReference(referencedSymbol.Definition),
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
                    : await context.ToolExecutionServices.InspectionContextService.TryCreateContainingSymbolAsync(reference.Document, reference.Location.SourceSpan.Start, cancellationToken);
                var contextLine = request.IncludeContext
                    ? await context.ToolExecutionServices.InspectionContextService.ReadContextAsync(reference.Document, reference.Location.SourceSpan, cancellationToken)
                    : null;

                var location = context.WorkspaceResolver.CreateResolvedLocation(reference.Location);
                if (location is null)
                {
                    continue;
                }

                references.Add(new ContractReferenceLocation
                {
                    Location = location,
                    ContainingSymbol = containingSymbol is null ? null : context.WorkspaceResolver.CreateSymbolReference(containingSymbol),
                    IsWrite = await IsWriteReferenceAsync(reference, cancellationToken),
                    Context = contextLine,
                });
            }
        }

        var orderedReferences = references
            .OrderBy(static reference => reference.Location?.Document?.Path, StringComparer.Ordinal)
            .ThenBy(static reference => reference.Location?.Span?.Start)
            .ToArray();
        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);

        return PluginExecutionResult<ReferenceSearchData>.Success(new ReferenceSearchData
        {
            Symbol = symbolReference,
            References = ToolExecutionHelpers.CreateBoundedCollection(
                orderedReferences,
                ToolExecutionHelpers.GetMaxResults(context, request.ReferencesLimit)),
        });
    }

    private static async ValueTask<bool> IsWriteReferenceAsync(Microsoft.CodeAnalysis.FindSymbols.ReferenceLocation reference, CancellationToken cancellationToken)
    {
        if (reference.Document is null)
        {
            return false;
        }

        var syntaxRoot = await reference.Document.GetSyntaxRootAsync(cancellationToken);
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
