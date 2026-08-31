using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Resolves plugin attributes and precise source locations for analyzer diagnostics.
/// </summary>
internal static class PluginAttributeFacts
{
    /// <summary>
    /// Finds an attribute of the requested type on a symbol.
    /// </summary>
    /// <param name="symbol">The symbol whose attributes are searched.</param>
    /// <param name="attributeType">The attribute type to match using symbol identity.</param>
    /// <returns>The matching attribute, or <see langword="null"/> when the symbol does not declare it.</returns>
    public static AttributeData? FindAttribute(
        ISymbol symbol,
        INamedTypeSymbol attributeType)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the source location of an attribute application.
    /// </summary>
    /// <param name="attribute">The attribute metadata.</param>
    /// <param name="cancellationToken">A token that cancels syntax retrieval.</param>
    /// <returns>The attribute's source location, or <see langword="null"/> for metadata-only attributes.</returns>
    public static Location? GetApplicationLocation(
        AttributeData attribute,
        CancellationToken cancellationToken)
    {
        var syntaxReference = attribute.ApplicationSyntaxReference;
        if (syntaxReference is null)
        {
            return null;
        }

        var syntax = syntaxReference.GetSyntax(cancellationToken);
        var location = syntax.GetLocation();
        return location;
    }

    /// <summary>
    /// Gets the most precise available source location for an attribute constructor argument.
    /// </summary>
    /// <param name="attribute">The attribute metadata.</param>
    /// <param name="parameterOrdinal">The constructor parameter whose supplied argument is located.</param>
    /// <param name="cancellationToken">A token that cancels syntax retrieval.</param>
    /// <returns>The matching argument location, the attribute location as a fallback, or <see langword="null"/> when no source application exists.</returns>
    public static Location? GetConstructorArgumentLocation(
        AttributeData attribute,
        int parameterOrdinal,
        CancellationToken cancellationToken)
    {
        var constructor = attribute.AttributeConstructor;
        if (constructor is null || parameterOrdinal >= constructor.Parameters.Length)
        {
            var applicationLocation = GetApplicationLocation(attribute, cancellationToken);
            return applicationLocation;
        }

        var syntaxReference = attribute.ApplicationSyntaxReference;
        if (syntaxReference is null)
        {
            return null;
        }

        var attributeSyntax = syntaxReference.GetSyntax(cancellationToken);
        if (attributeSyntax is not AttributeSyntax syntax || syntax.ArgumentList is null)
        {
            var applicationLocation = GetApplicationLocation(attribute, cancellationToken);
            return applicationLocation;
        }

        var parameterName = constructor.Parameters[parameterOrdinal].Name;
        var positionalOrdinal = 0;
        foreach (var argument in syntax.ArgumentList.Arguments)
        {
            if (argument.NameEquals is not null)
            {
                continue;
            }

            if (argument.NameColon is not null)
            {
                var namedParameter = argument.NameColon.Name.Identifier.ValueText;
                if (string.Equals(namedParameter, parameterName, StringComparison.Ordinal))
                {
                    var argumentLocation = argument.Expression.GetLocation();
                    return argumentLocation;
                }

                continue;
            }

            if (positionalOrdinal == parameterOrdinal)
            {
                var argumentLocation = argument.Expression.GetLocation();
                return argumentLocation;
            }

            positionalOrdinal++;
        }

        var fallbackLocation = GetApplicationLocation(attribute, cancellationToken);
        return fallbackLocation;
    }
}
