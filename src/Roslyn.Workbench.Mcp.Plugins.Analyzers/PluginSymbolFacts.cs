using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

internal static class PluginSymbolFacts
{
    public static bool CompilationDeclaresPlugin(
        INamespaceSymbol rootNamespace,
        INamedTypeSymbol pluginAttributeType)
    {
        var namespaces = new Stack<INamespaceSymbol>();
        namespaces.Push(rootNamespace);
        while (namespaces.Count > 0)
        {
            var currentNamespace = namespaces.Pop();
            foreach (var childNamespace in currentNamespace.GetNamespaceMembers())
            {
                namespaces.Push(childNamespace);
            }

            foreach (var type in currentNamespace.GetTypeMembers())
            {
                if (TypeOrNestedTypeDeclaresPlugin(type, pluginAttributeType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static Location? FindSourceLocation(ISymbol symbol)
    {
        foreach (var location in symbol.Locations)
        {
            if (location.IsInSource)
            {
                return location;
            }
        }

        return null;
    }

    public static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceType)
    {
        foreach (var candidate in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, interfaceType))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsOrImplementsInterface(ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, interfaceType))
        {
            return true;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return ImplementsInterface(namedType, interfaceType);
    }

    private static bool TypeOrNestedTypeDeclaresPlugin(
        INamedTypeSymbol type,
        INamedTypeSymbol pluginAttributeType)
    {
        if (HasSourceLocation(type) && HasAttribute(type, pluginAttributeType))
        {
            return true;
        }

        foreach (var nestedType in type.GetTypeMembers())
        {
            if (TypeOrNestedTypeDeclaresPlugin(nestedType, pluginAttributeType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSourceLocation(INamedTypeSymbol type)
    {
        return FindSourceLocation(type) is not null;
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }
}
