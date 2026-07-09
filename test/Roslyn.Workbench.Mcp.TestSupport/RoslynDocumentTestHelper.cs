namespace Roslyn.Workbench.Mcp.TestSupport;

/// <summary>
/// Provides reusable Roslyn document lookup helpers for unit tests that need real in-memory Roslyn state.
/// </summary>
public static class RoslynDocumentTestHelper
{
    /// <summary>
    /// Resolves the location for a single syntax node that matches the supplied predicate.
    /// </summary>
    /// <typeparam name="TNode">The syntax node type to search for.</typeparam>
    /// <param name="document">The Roslyn document to inspect.</param>
    /// <param name="predicate">The predicate used to select the target node.</param>
    /// <param name="cancellationToken">The cancellation token for the Roslyn lookup.</param>
    /// <returns>The Roslyn location for the matching node.</returns>
    public static async Task<Location> GetSingleNodeLocationAsync<TNode>(Document document, Func<TNode, bool> predicate, CancellationToken cancellationToken)
        where TNode : SyntaxNode
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(predicate);

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The syntax root for '{document.Name}' could not be resolved.");
        var node = syntaxRoot
            .DescendantNodes()
            .OfType<TNode>()
            .Single(predicate);

        return node.GetLocation();
    }

    /// <summary>
    /// Resolves a declared method symbol from a Roslyn document.
    /// </summary>
    /// <param name="document">The Roslyn document to inspect.</param>
    /// <param name="methodName">The method name to resolve.</param>
    /// <param name="containingTypeName">The optional containing type name.</param>
    /// <param name="cancellationToken">The cancellation token for the Roslyn lookup.</param>
    /// <returns>The resolved method symbol.</returns>
    public static async Task<IMethodSymbol> GetRequiredMethodSymbolAsync(
        Document document,
        string methodName,
        string? containingTypeName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The syntax root for '{document.Name}' could not be resolved.");
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The semantic model for '{document.Name}' could not be resolved.");
        var method = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(item => item.Identifier.ValueText == methodName
                && (containingTypeName is null || ((TypeDeclarationSyntax)item.Parent!).Identifier.ValueText == containingTypeName));

        return (IMethodSymbol)(semanticModel.GetDeclaredSymbol(method, cancellationToken)
            ?? throw new InvalidOperationException($"The method '{methodName}' could not be resolved."));
    }

    /// <summary>
    /// Resolves a declared property symbol from a Roslyn document.
    /// </summary>
    /// <param name="document">The Roslyn document to inspect.</param>
    /// <param name="propertyName">The property name to resolve.</param>
    /// <param name="cancellationToken">The cancellation token for the Roslyn lookup.</param>
    /// <returns>The resolved property symbol.</returns>
    public static async Task<IPropertySymbol> GetRequiredPropertySymbolAsync(Document document, string propertyName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The syntax root for '{document.Name}' could not be resolved.");
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The semantic model for '{document.Name}' could not be resolved.");
        var property = syntaxRoot.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Single(item => item.Identifier.ValueText == propertyName);

        return (IPropertySymbol)(semanticModel.GetDeclaredSymbol(property, cancellationToken)
            ?? throw new InvalidOperationException($"The property '{propertyName}' could not be resolved."));
    }

    /// <summary>
    /// Resolves a declared named type symbol from a Roslyn document.
    /// </summary>
    /// <param name="document">The Roslyn document to inspect.</param>
    /// <param name="typeName">The type name to resolve.</param>
    /// <param name="cancellationToken">The cancellation token for the Roslyn lookup.</param>
    /// <returns>The resolved named type symbol.</returns>
    public static async Task<INamedTypeSymbol> GetRequiredNamedTypeSymbolAsync(Document document, string typeName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The syntax root for '{document.Name}' could not be resolved.");
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The semantic model for '{document.Name}' could not be resolved.");
        var type = syntaxRoot.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Single(item => item.Identifier.ValueText == typeName);

        return (INamedTypeSymbol)(semanticModel.GetDeclaredSymbol(type, cancellationToken)
            ?? throw new InvalidOperationException($"The type '{typeName}' could not be resolved."));
    }
}
