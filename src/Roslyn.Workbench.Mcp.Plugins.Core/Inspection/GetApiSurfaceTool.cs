namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-api-surface", "Get API Surface", "Returns exported API symbols for a selected scope.")]
internal sealed class GetApiSurfaceTool : QueryToolHandler<GetApiSurfaceRequest, ApiSurfaceData>
{
    private sealed class AccessibilityThreshold
    {
        private AccessibilityThreshold()
        {
        }

        public static AccessibilityThreshold Public { get; } = new();

        public static AccessibilityThreshold Protected { get; } = new();

        public static AccessibilityThreshold Internal { get; } = new();
    }

    protected override async ValueTask<PluginExecutionResult<ApiSurfaceData>> ExecuteCoreAsync(GetApiSurfaceRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<ApiSurfaceData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var threshold = ParseMinimumAccessibility(request.MinimumAccessibility);
        if (threshold is null)
        {
            return ToolExecutionHelpers.Rejected<ApiSurfaceData>("InvalidRequest", "Minimum accessibility must be Public, Protected, or Internal.");
        }

        var exportedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var document in documents.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            foreach (var declaration in syntaxRoot.DescendantNodes().Where(IsApiDeclarationNode))
            {
                var symbol = GetDeclaredSymbol(semanticModel, declaration, cancellationToken);
                if (symbol is null || symbol.IsImplicitlyDeclared)
                {
                    continue;
                }

                if (!MeetsAccessibilityThreshold(symbol, threshold))
                {
                    continue;
                }

                if (!request.IncludeObsolete && HasObsoleteAttribute(symbol))
                {
                    continue;
                }

                exportedSymbols.Add(symbol);
            }
        }

        var orderedSymbols = exportedSymbols
            .OrderBy(item => context.WorkspaceResolver.CreateSymbolReference(item).DisplayName, StringComparer.Ordinal)
            .Select(item => new ApiSymbolInfo
            {
                Symbol = context.WorkspaceResolver.CreateSymbolReference(item),
                Accessibility = item.DeclaredAccessibility.ToString(),
                IsObsolete = HasObsoleteAttribute(item),
            })
            .ToArray();

        return PluginExecutionResult<ApiSurfaceData>.Success(new ApiSurfaceData
        {
            Symbols = ToolExecutionHelpers.CreateBoundedCollection(
                orderedSymbols,
                ToolExecutionHelpers.GetMaxResults(context, request.SymbolsLimit)),
        });
    }

    private static AccessibilityThreshold? ParseMinimumAccessibility(string value)
    {
        return value switch
        {
            "Public" => AccessibilityThreshold.Public,
            "Protected" => AccessibilityThreshold.Protected,
            "Internal" => AccessibilityThreshold.Internal,
            _ => null,
        };
    }

    private static ISymbol? GetDeclaredSymbol(SemanticModel semanticModel, SyntaxNode declaration, CancellationToken cancellationToken)
    {
        return declaration switch
        {
            BaseTypeDeclarationSyntax typeDeclarationSyntax => semanticModel.GetDeclaredSymbol(typeDeclarationSyntax, cancellationToken),
            DelegateDeclarationSyntax delegateDeclarationSyntax => semanticModel.GetDeclaredSymbol(delegateDeclarationSyntax, cancellationToken),
            BaseMethodDeclarationSyntax methodDeclarationSyntax => semanticModel.GetDeclaredSymbol(methodDeclarationSyntax, cancellationToken),
            PropertyDeclarationSyntax propertyDeclarationSyntax => semanticModel.GetDeclaredSymbol(propertyDeclarationSyntax, cancellationToken),
            EventDeclarationSyntax eventDeclarationSyntax => semanticModel.GetDeclaredSymbol(eventDeclarationSyntax, cancellationToken),
            VariableDeclaratorSyntax variableDeclaratorSyntax when variableDeclaratorSyntax.Parent?.Parent is FieldDeclarationSyntax => semanticModel.GetDeclaredSymbol(variableDeclaratorSyntax, cancellationToken),
            _ => null,
        };
    }

    private static bool HasObsoleteAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(static attribute => string.Equals(attribute.AttributeClass?.Name, "ObsoleteAttribute", StringComparison.Ordinal));
    }

    private static bool IsApiDeclarationNode(SyntaxNode node)
    {
        return node is BaseTypeDeclarationSyntax
            or DelegateDeclarationSyntax
            or BaseMethodDeclarationSyntax
            or PropertyDeclarationSyntax
            or EventDeclarationSyntax
            or VariableDeclaratorSyntax;
    }

    private static bool MeetsAccessibilityThreshold(ISymbol symbol, AccessibilityThreshold threshold)
    {
        var accessibilities = GetAccessibilityChain(symbol);

        if (ReferenceEquals(threshold, AccessibilityThreshold.Public))
        {
            return accessibilities.All(static accessibility => accessibility == Accessibility.Public);
        }

        if (ReferenceEquals(threshold, AccessibilityThreshold.Protected))
        {
            return accessibilities.All(static accessibility => accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal or Accessibility.ProtectedAndInternal);
        }

        return accessibilities.All(static accessibility => accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.Internal or Accessibility.ProtectedOrInternal or Accessibility.ProtectedAndInternal);
    }

    private static IReadOnlyList<Accessibility> GetAccessibilityChain(ISymbol symbol)
    {
        var result = new List<Accessibility>();
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.NotApplicable)
            {
                result.Add(current.DeclaredAccessibility);
            }
        }

        return result;
    }
}
