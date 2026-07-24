using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

internal static class PluginAttributeFacts
{
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
