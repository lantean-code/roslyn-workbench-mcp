namespace Roslyn.Workbench.Mcp.Plugins.Core.Projections;

/// <summary>
/// Builds a bounded semantic outline from document syntax and declared symbols.
/// </summary>
internal static class DocumentOutlineProjectionFactory
{
    /// <summary>
    /// Projects bounded outline children beneath a syntax node.
    /// </summary>
    /// <param name="syntaxNode">The syntax node whose children should be projected.</param>
    /// <param name="semanticModel">The semantic model used to identify declared symbols.</param>
    /// <param name="resolver">The resolver used to create canonical source locations.</param>
    /// <param name="includeMembers">Whether member declarations should be included.</param>
    /// <param name="maxNodes">The maximum total number of nodes to project.</param>
    /// <param name="maxDepth">The maximum outline depth to traverse.</param>
    /// <param name="truncated">Receives whether node or depth limits omitted outline content.</param>
    /// <param name="cancellationToken">The token that cancels outline projection.</param>
    /// <returns>The projected child outline nodes.</returns>
    public static OutlineNode[] BuildOutlineChildren(SyntaxNode syntaxNode, SemanticModel semanticModel, IWorkspaceResolver resolver, bool includeMembers, int maxNodes, int maxDepth, out bool truncated, CancellationToken cancellationToken)
    {
        var projectedNodeCount = 0;
        truncated = false;

        return BuildOutlineChildren(
            syntaxNode,
            semanticModel,
            resolver,
            includeMembers,
            maxNodes,
            maxDepth,
            currentDepth: 1,
            ref projectedNodeCount,
            ref truncated,
            cancellationToken);
    }

    private static OutlineNode[] BuildOutlineChildren(SyntaxNode syntaxNode, SemanticModel semanticModel, IWorkspaceResolver resolver, bool includeMembers, int maxNodes, int maxDepth, int currentDepth, ref int projectedNodeCount, ref bool truncated, CancellationToken cancellationToken)
    {
        var children = new List<OutlineNode>();
        foreach (var childNode in syntaxNode.ChildNodes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (childNode is BaseFieldDeclarationSyntax fieldDeclaration)
            {
                foreach (var variable in fieldDeclaration.Declaration.Variables)
                {
                    var fieldSymbol = semanticModel.GetDeclaredSymbol(variable, cancellationToken);
                    if (fieldSymbol is not null)
                    {
                        if (projectedNodeCount == maxNodes)
                        {
                            truncated = true;
                            return children.ToArray();
                        }

                        children.Add(CreateOutlineNode(fieldSymbol, resolver, []));
                        projectedNodeCount++;
                    }
                }

                continue;
            }

            var symbol = GetDeclaredSymbol(childNode, semanticModel, cancellationToken);
            if (symbol is null)
            {
                continue;
            }

            if (currentDepth > maxDepth || projectedNodeCount == maxNodes)
            {
                truncated = true;
                return children.ToArray();
            }

            projectedNodeCount++;
            IReadOnlyList<OutlineNode> nestedChildren = [];
            if (includeMembers || symbol is INamespaceSymbol)
            {
                if (currentDepth == maxDepth)
                {
                    truncated |= HasPotentialOutlineChildren(childNode);
                }
                else
                {
                    nestedChildren = BuildOutlineChildren(
                        childNode,
                        semanticModel,
                        resolver,
                        includeMembers,
                        maxNodes,
                        maxDepth,
                        currentDepth + 1,
                        ref projectedNodeCount,
                        ref truncated,
                        cancellationToken);
                }
            }

            children.Add(CreateOutlineNode(symbol, resolver, nestedChildren));

            if (truncated && projectedNodeCount == maxNodes)
            {
                return children.ToArray();
            }
        }

        return children.ToArray();
    }

    private static ISymbol? GetDeclaredSymbol(SyntaxNode syntaxNode, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        return syntaxNode switch
        {
            BaseNamespaceDeclarationSyntax namespaceDeclarationSyntax => semanticModel.GetDeclaredSymbol(namespaceDeclarationSyntax, cancellationToken),
            BaseTypeDeclarationSyntax typeDeclarationSyntax => semanticModel.GetDeclaredSymbol(typeDeclarationSyntax, cancellationToken),
            DelegateDeclarationSyntax delegateDeclarationSyntax => semanticModel.GetDeclaredSymbol(delegateDeclarationSyntax, cancellationToken),
            EnumMemberDeclarationSyntax enumMemberDeclarationSyntax => semanticModel.GetDeclaredSymbol(enumMemberDeclarationSyntax, cancellationToken),
            BaseMethodDeclarationSyntax methodDeclarationSyntax => semanticModel.GetDeclaredSymbol(methodDeclarationSyntax, cancellationToken),
            BasePropertyDeclarationSyntax propertyDeclarationSyntax => semanticModel.GetDeclaredSymbol(propertyDeclarationSyntax, cancellationToken),
            _ => null,
        };
    }

    private static bool HasPotentialOutlineChildren(SyntaxNode syntaxNode)
    {
        foreach (var childNode in syntaxNode.ChildNodes())
        {
            if (childNode is BaseFieldDeclarationSyntax { Declaration.Variables.Count: > 0 }
                || childNode is BaseNamespaceDeclarationSyntax
                || childNode is BaseTypeDeclarationSyntax
                || childNode is DelegateDeclarationSyntax
                || childNode is EnumMemberDeclarationSyntax
                || childNode is BaseMethodDeclarationSyntax
                || childNode is BasePropertyDeclarationSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static OutlineNode CreateOutlineNode(ISymbol symbol, IWorkspaceResolver resolver, IReadOnlyList<OutlineNode> children)
    {
        return new OutlineNode
        {
            Name = symbol.Name,
            Kind = symbol.Kind.ToString(),
            Accessibility = symbol.DeclaredAccessibility.ToString(),
            Modifiers = InspectionProjectionFactory.GetModifiers(symbol),
            Location = symbol.Locations.FirstOrDefault(static location => location.IsInSource) is { } location
                ? resolver.CreateResolvedLocation(location)
                : null,
            Children = children,
        };
    }
}
