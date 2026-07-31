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
                        || IsDisposed(localDeclaration, localSymbol, semanticModel, cancellationToken))
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
        return SymbolEqualityComparer.Default.Equals(type, disposable)
            || SymbolEqualityComparer.Default.Equals(type, asyncDisposable)
            || type.AllInterfaces.Any(interfaceType =>
                SymbolEqualityComparer.Default.Equals(interfaceType, disposable)
                || SymbolEqualityComparer.Default.Equals(interfaceType, asyncDisposable));
    }

    private static bool IsDisposed(
        LocalDeclarationStatementSyntax localDeclaration,
        ILocalSymbol localSymbol,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var statementScope = localDeclaration.Parent;
        if (statementScope is null)
        {
            return false;
        }

        foreach (var invocation in statementScope.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Name.Identifier.ValueText is not ("Dispose" or "DisposeAsync")
                || invocation.FirstAncestorOrSelf<ExpressionStatementSyntax>() is not { } disposalStatement
                || GetDisposalRegionEnd(disposalStatement, statementScope) is not { } disposalRegionEnd
                || disposalRegionEnd.SpanStart <= localDeclaration.SpanStart)
            {
                continue;
            }

            var receiverSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;
            if (!SymbolEqualityComparer.Default.Equals(receiverSymbol, localSymbol))
            {
                continue;
            }

            var controlFlow = semanticModel.AnalyzeControlFlow(localDeclaration, disposalRegionEnd);
            if (controlFlow is not null && controlFlow.ExitPoints.Length == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static StatementSyntax? GetDisposalRegionEnd(ExpressionStatementSyntax disposalStatement, SyntaxNode statementScope)
    {
        if (ReferenceEquals(disposalStatement.Parent, statementScope))
        {
            return disposalStatement;
        }

        return disposalStatement.Parent is BlockSyntax
        {
            Parent: FinallyClauseSyntax
            {
                Parent: TryStatementSyntax tryStatement,
            },
        }
            && ReferenceEquals(tryStatement.Parent, statementScope)
                ? tryStatement
                : null;
    }
}
