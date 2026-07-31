namespace Roslyn.Workbench.Mcp.Plugins.Core.Projections;

internal static class DocumentOutlineProjectionFactory
{
    public static OutlineNode[] BuildOutlineChildren(SyntaxNode syntaxNode, SemanticModel semanticModel, IWorkspaceResolver resolver, bool includeMembers, CancellationToken cancellationToken)
    {
        var children = new List<OutlineNode>();
        foreach (var childNode in syntaxNode.ChildNodes())
        {
            if (childNode is BaseFieldDeclarationSyntax fieldDeclaration)
            {
                foreach (var variable in fieldDeclaration.Declaration.Variables)
                {
                    var fieldSymbol = semanticModel.GetDeclaredSymbol(variable, cancellationToken);
                    if (fieldSymbol is not null)
                    {
                        children.Add(CreateOutlineNode(fieldSymbol, resolver, []));
                    }
                }

                continue;
            }

            var child = CreateOutlineNode(childNode, semanticModel, resolver, includeMembers, cancellationToken);
            if (child is not null)
            {
                children.Add(child);
            }
        }

        return children.ToArray();
    }

    private static OutlineNode? CreateOutlineNode(SyntaxNode syntaxNode, SemanticModel semanticModel, IWorkspaceResolver resolver, bool includeMembers, CancellationToken cancellationToken)
    {
        var symbol = syntaxNode switch
        {
            BaseNamespaceDeclarationSyntax namespaceDeclarationSyntax => semanticModel.GetDeclaredSymbol(namespaceDeclarationSyntax, cancellationToken),
            BaseTypeDeclarationSyntax typeDeclarationSyntax => semanticModel.GetDeclaredSymbol(typeDeclarationSyntax, cancellationToken),
            DelegateDeclarationSyntax delegateDeclarationSyntax => semanticModel.GetDeclaredSymbol(delegateDeclarationSyntax, cancellationToken),
            EnumMemberDeclarationSyntax enumMemberDeclarationSyntax => semanticModel.GetDeclaredSymbol(enumMemberDeclarationSyntax, cancellationToken),
            BaseMethodDeclarationSyntax methodDeclarationSyntax => semanticModel.GetDeclaredSymbol(methodDeclarationSyntax, cancellationToken),
            BasePropertyDeclarationSyntax propertyDeclarationSyntax => semanticModel.GetDeclaredSymbol(propertyDeclarationSyntax, cancellationToken),
            _ => null,
        };

        if (symbol is null)
        {
            return null;
        }

        var children = includeMembers || symbol is INamespaceSymbol
            ? BuildOutlineChildren(syntaxNode, semanticModel, resolver, includeMembers, cancellationToken)
            : [];

        return CreateOutlineNode(symbol, resolver, children);
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
