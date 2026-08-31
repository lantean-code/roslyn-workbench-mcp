using Roslyn.Workbench.Mcp.Workspace.Diagnostics;

using ContractReferenceLocation = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.ReferenceLocation;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Finds source references with optional declaration and access classification.
/// </summary>
[RoslynTool(_toolName, "Find References", "Finds source references, optionally including declarations and access classification.")]
internal sealed class FindReferencesTool : QueryToolHandler<FindReferencesRequest, ReferenceSearchData>
{
    private const string _toolName = "find-references";

    /// <inheritdoc/>
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

        var discoveredReferences = await context.ToolExecutionServices.ReferenceDiscoveryService.FindReferencesAsync(
            context.WorkspaceIdentity.WorkspaceId,
            context.CurrentSolution,
            symbol,
            documents.Value,
            request.IncludeDefinitions,
            cancellationToken);

        var referenceCandidates = new List<ReferenceSearchCandidate>();
        foreach (var discoveredReference in discoveredReferences)
        {
            var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(
                discoveredReference.Location);

            if (resolvedLocation is null)
            {
                continue;
            }

            referenceCandidates.Add(new ReferenceSearchCandidate
            {
                Occurrence = discoveredReference,
                ResolvedLocation = resolvedLocation,
            });
        }

        var maxResults = request.EffectiveReferencesLimit;
        var selectedReferences = new List<ReferenceSearchCandidate>();
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.ResultSelectionPhase))
        {
            var orderedReferences = referenceCandidates
                .OrderBy(static reference => reference.ResolvedLocation.Document?.Path, StringComparer.Ordinal)
                .ThenBy(static reference => reference.ResolvedLocation.Span?.Start)
                .ThenBy(static reference => reference.ResolvedLocation.Document?.ProjectId, StringComparer.Ordinal)
                .ThenBy(static reference => reference.ResolvedLocation.Document?.DocumentId, StringComparer.Ordinal)
                .ThenBy(static reference => reference.ResolvedLocation.Span?.Length)
                .ThenBy(static reference => reference.Occurrence.IsDefinition);

            foreach (var reference in orderedReferences)
            {
                if (selectedReferences.Count == maxResults)
                {
                    break;
                }

                selectedReferences.Add(reference);
            }
        }

        var references = new List<ContractReferenceLocation>();
        using (WorkbenchPerformanceEventSource.Log.StartPhase(_toolName, WorkbenchPerformanceEventSource.ResultEnrichmentPhase))
        {
            foreach (var selectedReference in selectedReferences)
            {
                var occurrence = selectedReference.Occurrence;
                if (occurrence.IsDefinition)
                {
                    references.Add(new ContractReferenceLocation
                    {
                        Location = selectedReference.ResolvedLocation,
                        ContainingSymbol = context.WorkspaceResolver.CreateSymbolReference(occurrence.Definition),
                        IsDefinition = true,
                    });

                    continue;
                }

                var containingSymbol = await context.ToolExecutionServices.InspectionContextService.TryCreateContainingSymbolAsync(
                    occurrence.Document,
                    occurrence.Location.SourceSpan.Start,
                    cancellationToken);

                string? contextLine = null;
                if (request.IncludeContext)
                {
                    contextLine = await context.ToolExecutionServices.InspectionContextService.ReadContextAsync(
                        occurrence.Document,
                        occurrence.Location.SourceSpan,
                        cancellationToken);
                }

                references.Add(new ContractReferenceLocation
                {
                    Location = selectedReference.ResolvedLocation,
                    ContainingSymbol = containingSymbol is null ? null : context.WorkspaceResolver.CreateSymbolReference(containingSymbol),
                    IsWrite = await IsWriteReferenceAsync(occurrence.Document, occurrence.Location, cancellationToken),
                    Context = contextLine,
                });
            }
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);

        var data = new ReferenceSearchData
        {
            Symbol = symbolReference,
            References = BoundedCollection.CreatePrebounded(
                references,
                referenceCandidates.Count),
        };

        return PluginExecutionResult.Success(data);
    }

    private static async ValueTask<bool> IsWriteReferenceAsync(Document document, Location location, CancellationToken cancellationToken)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        if (syntaxRoot is null)
        {
            return false;
        }

        var node = syntaxRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
        for (SyntaxNode? current = node; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AssignmentExpressionSyntax assignmentExpressionSyntax
                    when assignmentExpressionSyntax.Left.Span.Contains(location.SourceSpan):
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
