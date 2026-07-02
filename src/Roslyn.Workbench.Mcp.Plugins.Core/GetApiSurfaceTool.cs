using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class GetApiSurfaceTool : QueryToolHandler<GetApiSurfaceRequest, ApiSurfaceData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-api-surface",
        Title = "Get API Surface",
        Description = "Returns exported API symbols for a selected scope.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetApiSurfaceTool());
    }

    protected override async ValueTask<PluginExecutionResult<ApiSurfaceData>> ExecuteCoreAsync(GetApiSurfaceRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var documents = ToolExecutionHelpers.ResolveDocuments<ApiSurfaceData>(request.Scope, context);
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
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
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

                if (!MeetsAccessibilityThreshold(symbol, threshold.Value))
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
            .OrderBy(item => context.Resolver.CreateSymbolReference(item).DisplayName, StringComparer.Ordinal)
            .Select(item => new ApiSymbolInfo
            {
                Symbol = context.Resolver.CreateSymbolReference(item),
                Accessibility = item.DeclaredAccessibility.ToString(),
                IsObsolete = HasObsoleteAttribute(item),
            })
            .ToArray();

        return ToolExecutionHelpers.CreateBoundedCollectionResult(
            context,
            orderedSymbols,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            (items, hasMore) => new ApiSurfaceData
            {
                Symbols = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }

    private static Accessibility? ParseMinimumAccessibility(string value)
    {
        return value switch
        {
            "Public" => Accessibility.Public,
            "Protected" => Accessibility.Protected,
            "Internal" => Accessibility.Internal,
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

    private static bool MeetsAccessibilityThreshold(ISymbol symbol, Accessibility threshold)
    {
        var accessibilities = GetAccessibilityChain(symbol);

        return threshold switch
        {
            Accessibility.Public => accessibilities.All(static accessibility => accessibility == Accessibility.Public),
            Accessibility.Protected => accessibilities.All(static accessibility => accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal or Accessibility.ProtectedAndInternal),
            Accessibility.Internal => accessibilities.All(static accessibility => accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.Internal or Accessibility.ProtectedOrInternal or Accessibility.ProtectedAndInternal),
            _ => false,
        };
    }

    private static IReadOnlyList<Accessibility> GetAccessibilityChain(ISymbol symbol)
    {
        var result = new List<Accessibility>();
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current is INamespaceSymbol)
            {
                continue;
            }

            if (current.DeclaredAccessibility != Accessibility.NotApplicable)
            {
                result.Add(current.DeclaredAccessibility);
            }
        }

        return result;
    }
}
