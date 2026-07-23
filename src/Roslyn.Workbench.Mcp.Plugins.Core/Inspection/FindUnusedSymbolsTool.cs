namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-unused-symbols", "Find Unused Symbols", "Returns candidate unused locals and members from compiler diagnostics.")]
internal sealed class FindUnusedSymbolsTool : QueryToolHandler<FindUnusedSymbolsRequest, UnusedSymbolsData>
{
    protected override async ValueTask<PluginExecutionResult<UnusedSymbolsData>> ExecuteCoreAsync(FindUnusedSymbolsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {

        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<UnusedSymbolsData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var selectedDocuments = documents.Value.ToArray();
        if (request.ExcludeGenerated)
        {
            selectedDocuments = documents.Value
                .Where(static document => !CompilerDiagnosticHelpers.IsGeneratedDocument(document))
                .ToArray();
        }

        var diagnostics = await context.ToolExecutionServices.CompilerDiagnosticService.GetCompilerDiagnosticsAsync(selectedDocuments, cancellationToken);
        var maxResults = request.EffectiveCandidatesLimit;
        var candidates = new List<UnusedSymbolCandidate>();
        var hasMore = false;
        SyntaxTree? activeSyntaxTree = null;
        SyntaxNode? syntaxRoot = null;
        SemanticModel? semanticModel = null;
        var unusedDiagnostics = new List<Diagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (IsUnusedDiagnosticId(diagnostic.Id))
            {
                unusedDiagnostics.Add(diagnostic);
            }
        }

        var orderedDiagnostics = unusedDiagnostics
            .OrderBy(static diagnostic => diagnostic.Location.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal);

        foreach (var diagnostic in orderedDiagnostics)
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

            if (!ReferenceEquals(activeSyntaxTree, diagnostic.Location.SourceTree))
            {
                activeSyntaxTree = diagnostic.Location.SourceTree;
                syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
                semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            }

            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            var symbol = GetCandidateSymbol(syntaxRoot, semanticModel, diagnostic.Location.SourceSpan, cancellationToken);
            if (symbol is null || !ShouldIncludeSymbol(symbol, request.IncludeInternal))
            {
                continue;
            }

            if (candidates.Count == maxResults)
            {
                hasMore = true;
                break;
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
                    diagnostic.GetMessage(CultureInfo.InvariantCulture),
                ],
            });
        }

        var data = new UnusedSymbolsData
        {
            Candidates = BoundedCollection<UnusedSymbolCandidate>.CreatePrebounded(
                candidates,
                hasMore),
        };

        return PluginExecutionResult<UnusedSymbolsData>.Success(data);
    }

    private static bool IsUnusedDiagnosticId(string diagnosticId)
    {
        return diagnosticId is "CS0168" or "CS0169" or "CS0219";
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
            _ => symbol.DeclaredAccessibility == Accessibility.Private
                || (includeInternal && symbol.DeclaredAccessibility is Accessibility.Internal or Accessibility.ProtectedOrInternal or Accessibility.ProtectedAndInternal),
        };
    }
}
