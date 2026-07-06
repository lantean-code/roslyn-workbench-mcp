using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class FindUnusedSymbolsTool : QueryToolHandler<FindUnusedSymbolsRequest, UnusedSymbolsData>
{
    private static readonly HashSet<string> _unusedDiagnosticIds = new(StringComparer.Ordinal)
    {
        "CS0168",
        "CS0169",
        "CS0219",
    };

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "find-unused-symbols",
        Title = "Find Unused Symbols",
        Description = "Returns candidate unused locals and members from compiler diagnostics.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new FindUnusedSymbolsTool());
    }

    protected override async ValueTask<PluginExecutionResult<UnusedSymbolsData>> ExecuteCoreAsync(FindUnusedSymbolsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<UnusedSymbolsData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var selectedDocuments = request.ExcludeGenerated
            ? documents.Value.Where(static document => !CompilerDiagnosticHelpers.IsGeneratedDocument(document)).ToArray()
            : documents.Value.ToArray();
        var diagnostics = await context.ToolExecutionServices.CompilerDiagnosticService.GetCompilerDiagnosticsAsync(selectedDocuments, cancellationToken).ConfigureAwait(false);
        var candidates = new List<UnusedSymbolCandidate>();

        foreach (var diagnostic in diagnostics.Where(diagnostic => _unusedDiagnosticIds.Contains(diagnostic.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Location.SourceTree is null)
            {
                continue;
            }

            var document = context.CurrentSolution.GetDocument(diagnostic.Location.SourceTree);
            if (document is null)
            {
                continue;
            }

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            var symbol = GetCandidateSymbol(syntaxRoot, semanticModel, diagnostic.Location.SourceSpan, cancellationToken);
            if (symbol is null || !ShouldIncludeSymbol(symbol, request.IncludeInternal))
            {
                continue;
            }

            var sourceLocation = symbol.Locations.FirstOrDefault(static location => location.IsInSource);
            candidates.Add(new UnusedSymbolCandidate
            {
                Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
                Location = sourceLocation is null ? null : context.WorkspaceResolver.CreateResolvedLocation(sourceLocation),
                Confidence = "High",
                Reasons =
                [
                    diagnostic.Id,
                    diagnostic.GetMessage(),
                ],
            });
        }

        var orderedCandidates = candidates
            .OrderBy(static candidate => candidate.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Location?.Span?.Start ?? int.MaxValue)
            .ThenBy(static candidate => candidate.Symbol?.DisplayName ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        return context.ToolExecutionServices.ResultShaper.CreateBoundedCollectionResult(
            context,
            orderedCandidates,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            static (items, hasMore) => new UnusedSymbolsData
            {
                Candidates = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }

    private static ISymbol? GetCandidateSymbol(SyntaxNode syntaxRoot, SemanticModel semanticModel, TextSpan span, CancellationToken cancellationToken)
    {
        var node = syntaxRoot.FindNode(span, getInnermostNodeForTie: true);
        if (node.FirstAncestorOrSelf<VariableDeclaratorSyntax>() is { } variableDeclarator)
        {
            return semanticModel.GetDeclaredSymbol(variableDeclarator, cancellationToken);
        }

        if (node.FirstAncestorOrSelf<CatchDeclarationSyntax>() is { } catchDeclaration)
        {
            return semanticModel.GetDeclaredSymbol(catchDeclaration, cancellationToken);
        }

        return null;
    }

    private static bool ShouldIncludeSymbol(ISymbol symbol, bool includeInternal)
    {
        return symbol switch
        {
            ILocalSymbol => true,
            IRangeVariableSymbol => true,
            _ => symbol.DeclaredAccessibility == Accessibility.Private
                || (includeInternal && symbol.DeclaredAccessibility is Accessibility.Internal or Accessibility.ProtectedOrInternal or Accessibility.ProtectedAndInternal),
        };
    }
}
