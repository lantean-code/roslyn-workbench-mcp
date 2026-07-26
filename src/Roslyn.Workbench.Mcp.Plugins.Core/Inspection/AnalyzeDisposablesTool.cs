namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("analyze-disposables", "Analyze Disposables", "Returns advisory findings for undisposed local disposable values.")]
internal sealed class AnalyzeDisposablesTool : QueryToolHandler<AnalyzeDisposablesRequest, DisposableAnalysisData>
{
    protected override async ValueTask<PluginExecutionResult<DisposableAnalysisData>> ExecuteCoreAsync(AnalyzeDisposablesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<DisposableAnalysisData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var maxResults = request.EffectiveFindingsLimit;
        var findings = new List<DisposableFinding>();
        var typeSymbolCache = new CompilationTypeSymbolCache();
        var hasMore = false;
        foreach (var document in documents.Value.OrderBy(static item => item.FilePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            var compilation = semanticModel.Compilation;
            var disposable = typeSymbolCache.GetTypeByMetadataName(compilation, "System.IDisposable");
            var asyncDisposable = typeSymbolCache.GetTypeByMetadataName(compilation, "System.IAsyncDisposable");
            var disposedSymbolsByExecutable = new Dictionary<SyntaxNode, HashSet<ISymbol>>();

            foreach (var localDeclaration in syntaxRoot.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (localDeclaration.UsingKeyword != default || localDeclaration.Parent is UsingStatementSyntax)
                {
                    continue;
                }

                foreach (var variableDeclarator in localDeclaration.Declaration.Variables)
                {
                    if (semanticModel.GetDeclaredSymbol(variableDeclarator, cancellationToken) is not ILocalSymbol localSymbol
                        || !ImplementsDisposable(localSymbol.Type, disposable, asyncDisposable)
                        || IsDisposed(localDeclaration, localSymbol, semanticModel, disposedSymbolsByExecutable, cancellationToken))
                    {
                        continue;
                    }

                    if (findings.Count == maxResults)
                    {
                        hasMore = true;
                        break;
                    }

                    findings.Add(new DisposableFinding
                    {
                        Kind = "UndisposedLocal",
                        Symbol = context.WorkspaceResolver.CreateSymbolReference(localSymbol),
                        Type = InspectionProjectionFactory.CreateTypeInfo(localSymbol.Type),
                        Location = context.WorkspaceResolver.CreateResolvedLocation(variableDeclarator.GetLocation()),
                        Message = "The disposable local is not disposed before it goes out of scope.",
                    });
                }

                if (hasMore)
                {
                    break;
                }
            }

            if (hasMore)
            {
                break;
            }
        }

        var orderedFindings = findings
            .OrderBy(static finding => finding.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static finding => finding.Location?.Span?.Start ?? int.MaxValue)
            .ThenBy(static finding => finding.Type?.DisplayName ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        var data = new DisposableAnalysisData
        {
            Findings = BoundedCollection.CreatePrebounded(
                orderedFindings,
                hasMore),
        };

        return PluginExecutionResult.Success(data);
    }

    private static bool ImplementsDisposable(ITypeSymbol type, INamedTypeSymbol? disposable, INamedTypeSymbol? asyncDisposable)
    {
        return type.AllInterfaces.Any(interfaceType =>
            SymbolEqualityComparer.Default.Equals(interfaceType, disposable)
            || SymbolEqualityComparer.Default.Equals(interfaceType, asyncDisposable));
    }

    private static bool IsDisposed(
        LocalDeclarationStatementSyntax localDeclaration,
        ILocalSymbol localSymbol,
        SemanticModel semanticModel,
        Dictionary<SyntaxNode, HashSet<ISymbol>> disposedSymbolsByExecutable,
        CancellationToken cancellationToken)
    {
        var executableNode = localDeclaration.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>()?.Body;
        executableNode ??= localDeclaration.FirstAncestorOrSelf<LocalFunctionStatementSyntax>()?.Body;

        if (executableNode is null)
        {
            return false;
        }

        if (!disposedSymbolsByExecutable.TryGetValue(executableNode, out var disposedSymbols))
        {
            disposedSymbols = GetDisposedSymbols(executableNode, semanticModel, cancellationToken);
            disposedSymbolsByExecutable.Add(executableNode, disposedSymbols);
        }

        return disposedSymbols.Contains(localSymbol);
    }

    private static HashSet<ISymbol> GetDisposedSymbols(SyntaxNode executableNode, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var disposedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var invocation in executableNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            if (memberAccess.Name.Identifier.ValueText is not ("Dispose" or "DisposeAsync"))
            {
                continue;
            }

            var receiverSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;
            if (receiverSymbol is ILocalSymbol)
            {
                disposedSymbols.Add(receiverSymbol);
            }
        }

        return disposedSymbols;
    }
}
