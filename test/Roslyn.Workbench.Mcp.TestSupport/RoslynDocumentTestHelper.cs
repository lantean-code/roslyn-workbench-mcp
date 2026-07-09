using Microsoft.CodeAnalysis.Operations;

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

        var syntaxRoot = await GetRequiredSyntaxRootAsync(document, cancellationToken).ConfigureAwait(false);
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

        var (syntaxRoot, semanticModel) = await GetRequiredSyntaxRootAndSemanticModelAsync(document, cancellationToken).ConfigureAwait(false);
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

        var (syntaxRoot, semanticModel) = await GetRequiredSyntaxRootAndSemanticModelAsync(document, cancellationToken).ConfigureAwait(false);
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

        var (syntaxRoot, semanticModel) = await GetRequiredSyntaxRootAndSemanticModelAsync(document, cancellationToken).ConfigureAwait(false);
        var type = syntaxRoot.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Single(item => item.Identifier.ValueText == typeName);

        return (INamedTypeSymbol)(semanticModel.GetDeclaredSymbol(type, cancellationToken)
            ?? throw new InvalidOperationException($"The type '{typeName}' could not be resolved."));
    }

    /// <summary>
    /// Resolves the target symbol for a single invocation that matches the supplied predicate.
    /// </summary>
    /// <param name="document">The Roslyn document to inspect.</param>
    /// <param name="predicate">The predicate used to select the target invocation.</param>
    /// <param name="cancellationToken">The cancellation token for the Roslyn lookup.</param>
    /// <returns>The resolved target symbol.</returns>
    public static async Task<ISymbol> GetRequiredInvocationTargetSymbolAsync(
        Document document,
        Func<InvocationExpressionSyntax, bool> predicate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(predicate);

        var (syntaxRoot, semanticModel) = await GetRequiredSyntaxRootAndSemanticModelAsync(document, cancellationToken).ConfigureAwait(false);
        var invocation = syntaxRoot
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single(predicate);
        var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);

        return symbolInfo.Symbol
            ?? symbolInfo.CandidateSymbols.SingleOrDefault()
            ?? throw new InvalidOperationException($"The invocation '{invocation}' could not be resolved.");
    }

    /// <summary>
    /// Resolves a declared local function symbol from a Roslyn document.
    /// </summary>
    /// <param name="document">The Roslyn document to inspect.</param>
    /// <param name="functionName">The local function name to resolve.</param>
    /// <param name="cancellationToken">The cancellation token for the Roslyn lookup.</param>
    /// <returns>The resolved local function symbol.</returns>
    public static async Task<IMethodSymbol> GetRequiredLocalFunctionSymbolAsync(
        Document document,
        string functionName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        var (syntaxRoot, semanticModel) = await GetRequiredSyntaxRootAndSemanticModelAsync(document, cancellationToken).ConfigureAwait(false);
        var function = syntaxRoot
            .DescendantNodes()
            .OfType<LocalFunctionStatementSyntax>()
            .Single(item => item.Identifier.ValueText == functionName);

        return (IMethodSymbol)(semanticModel.GetDeclaredSymbol(function, cancellationToken)
            ?? throw new InvalidOperationException($"The local function '{functionName}' could not be resolved."));
    }

    /// <summary>
    /// Resolves the symbol for a single anonymous function from a Roslyn document.
    /// </summary>
    /// <param name="document">The Roslyn document to inspect.</param>
    /// <param name="cancellationToken">The cancellation token for the Roslyn lookup.</param>
    /// <returns>The resolved anonymous function symbol.</returns>
    public static async Task<IMethodSymbol> GetRequiredAnonymousFunctionSymbolAsync(Document document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var (syntaxRoot, semanticModel) = await GetRequiredSyntaxRootAndSemanticModelAsync(document, cancellationToken).ConfigureAwait(false);
        var anonymousFunction = syntaxRoot
            .DescendantNodes()
            .OfType<AnonymousFunctionExpressionSyntax>()
            .Single();
        var operation = semanticModel.GetOperation(anonymousFunction, cancellationToken) as IAnonymousFunctionOperation;

        return operation?.Symbol
            ?? throw new InvalidOperationException("The anonymous function could not be resolved.");
    }

    private static async Task<SyntaxNode> GetRequiredSyntaxRootAsync(Document document, CancellationToken cancellationToken)
    {
        return await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The syntax root for '{document.Name}' could not be resolved.");
    }

    private static async Task<SemanticModel> GetRequiredSemanticModelAsync(Document document, CancellationToken cancellationToken)
    {
        return await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The semantic model for '{document.Name}' could not be resolved.");
    }

    private static async Task<(SyntaxNode SyntaxRoot, SemanticModel SemanticModel)> GetRequiredSyntaxRootAndSemanticModelAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var syntaxRoot = await GetRequiredSyntaxRootAsync(document, cancellationToken).ConfigureAwait(false);
        var semanticModel = await GetRequiredSemanticModelAsync(document, cancellationToken).ConfigureAwait(false);

        return (syntaxRoot, semanticModel);
    }
}
