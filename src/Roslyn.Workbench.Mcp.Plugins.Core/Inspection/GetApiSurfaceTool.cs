namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns exported API symbols for a selected scope.
/// </summary>
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

    /// <inheritdoc/>
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
            return PluginExecutionResult.Rejected<ApiSurfaceData>("InvalidRequest", "Minimum accessibility must be Public, Protected, or Internal.");
        }

        var maxResults = request.EffectiveSymbolsLimit;
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

            foreach (var declaration in syntaxRoot.DescendantNodes())
            {
                if (!IsApiDeclarationNode(declaration))
                {
                    continue;
                }

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

        var orderedSymbols = new List<(ISymbol Symbol, string SortKey)>();
        foreach (var symbol in exportedSymbols)
        {
            var sortKey = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            orderedSymbols.Add((symbol, sortKey));
        }

        orderedSymbols.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.SortKey, right.SortKey));

        var symbols = new List<ApiSymbolInfo>();
        foreach (var candidate in orderedSymbols)
        {
            if (symbols.Count == maxResults)
            {
                break;
            }

            symbols.Add(new ApiSymbolInfo
            {
                Symbol = context.WorkspaceResolver.CreateSymbolReference(candidate.Symbol),
                Accessibility = candidate.Symbol.DeclaredAccessibility.ToString(),
                IsObsolete = request.IncludeObsolete && HasObsoleteAttribute(candidate.Symbol),
            });
        }

        var data = new ApiSurfaceData
        {
            Symbols = BoundedCollection.CreatePrebounded(symbols, orderedSymbols.Count),
        };

        return PluginExecutionResult.Success(data);
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
            EnumMemberDeclarationSyntax enumMemberDeclarationSyntax => semanticModel.GetDeclaredSymbol(enumMemberDeclarationSyntax, cancellationToken),
            BaseMethodDeclarationSyntax methodDeclarationSyntax => semanticModel.GetDeclaredSymbol(methodDeclarationSyntax, cancellationToken),
            PropertyDeclarationSyntax propertyDeclarationSyntax => semanticModel.GetDeclaredSymbol(propertyDeclarationSyntax, cancellationToken),
            IndexerDeclarationSyntax indexerDeclarationSyntax => semanticModel.GetDeclaredSymbol(indexerDeclarationSyntax, cancellationToken),
            EventDeclarationSyntax eventDeclarationSyntax => semanticModel.GetDeclaredSymbol(eventDeclarationSyntax, cancellationToken),
            VariableDeclaratorSyntax variableDeclaratorSyntax when variableDeclaratorSyntax.Parent?.Parent is FieldDeclarationSyntax => semanticModel.GetDeclaredSymbol(variableDeclaratorSyntax, cancellationToken),
            VariableDeclaratorSyntax variableDeclaratorSyntax when variableDeclaratorSyntax.Parent?.Parent is EventFieldDeclarationSyntax => semanticModel.GetDeclaredSymbol(variableDeclaratorSyntax, cancellationToken),
            _ => null,
        };
    }

    private static bool HasObsoleteAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (string.Equals(attribute.AttributeClass?.Name, "ObsoleteAttribute", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsApiDeclarationNode(SyntaxNode node)
    {
        return node is BaseTypeDeclarationSyntax
            or DelegateDeclarationSyntax
            or EnumMemberDeclarationSyntax
            or BaseMethodDeclarationSyntax
            or PropertyDeclarationSyntax
            or IndexerDeclarationSyntax
            or EventDeclarationSyntax
            or VariableDeclaratorSyntax;
    }

    private static bool MeetsAccessibilityThreshold(ISymbol symbol, AccessibilityThreshold threshold)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            var accessibility = current.DeclaredAccessibility;
            if (accessibility == Accessibility.NotApplicable)
            {
                continue;
            }

            if (ReferenceEquals(threshold, AccessibilityThreshold.Public)
                && accessibility != Accessibility.Public)
            {
                return false;
            }

            if (ReferenceEquals(threshold, AccessibilityThreshold.Protected)
                && accessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal or Accessibility.ProtectedAndInternal))
            {
                return false;
            }

            if (ReferenceEquals(threshold, AccessibilityThreshold.Internal)
                && accessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.Internal or Accessibility.ProtectedOrInternal or Accessibility.ProtectedAndInternal))
            {
                return false;
            }
        }

        return true;
    }
}
