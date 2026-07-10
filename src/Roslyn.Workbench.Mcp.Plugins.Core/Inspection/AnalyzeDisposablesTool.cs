using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class AnalyzeDisposablesTool : QueryToolHandler<AnalyzeDisposablesRequest, DisposableAnalysisData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "analyze-disposables",
        Title = "Analyze Disposables",
        Description = "Returns advisory findings for undisposed local disposable values.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new AnalyzeDisposablesTool());
    }

    protected override async ValueTask<PluginExecutionResult<DisposableAnalysisData>> ExecuteCoreAsync(AnalyzeDisposablesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {

        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<DisposableAnalysisData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var findings = new List<DisposableFinding>();
        foreach (var document in documents.Value.OrderBy(static item => item.FilePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

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
                        || !ImplementsDisposable(localSymbol.Type, semanticModel.Compilation)
                        || IsDisposed(localDeclaration, localSymbol, semanticModel, cancellationToken))
                    {
                        continue;
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
            }
        }

        var orderedFindings = findings
            .OrderBy(static finding => finding.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static finding => finding.Location?.Span?.Start ?? int.MaxValue)
            .ThenBy(static finding => finding.Type?.DisplayName ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        return PluginExecutionResult<DisposableAnalysisData>.Success(new DisposableAnalysisData
        {
            Findings = ToolExecutionHelpers.CreateBoundedCollection(
                orderedFindings,
                ToolExecutionHelpers.GetMaxResults(context, request.FindingsLimit)),
        });
    }

    private static bool ImplementsDisposable(ITypeSymbol type, Compilation compilation)
    {
        var disposable = compilation.GetTypeByMetadataName("System.IDisposable");
        var asyncDisposable = compilation.GetTypeByMetadataName("System.IAsyncDisposable");

        return type.AllInterfaces.Any(interfaceType =>
            SymbolEqualityComparer.Default.Equals(interfaceType, disposable)
            || SymbolEqualityComparer.Default.Equals(interfaceType, asyncDisposable));
    }

    private static bool IsDisposed(LocalDeclarationStatementSyntax localDeclaration, ILocalSymbol localSymbol, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var executableNode = localDeclaration.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>()?.Body
            ?? (SyntaxNode?)localDeclaration.FirstAncestorOrSelf<LocalFunctionStatementSyntax>()?.Body;
        if (executableNode is null)
        {
            return false;
        }

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
            if (SymbolEqualityComparer.Default.Equals(receiverSymbol, localSymbol))
            {
                return true;
            }
        }

        return false;
    }
}
