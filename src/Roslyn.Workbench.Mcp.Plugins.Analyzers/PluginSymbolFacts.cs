using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Provides symbol-identity and source-origin checks shared by plugin analyzers.
/// </summary>
internal static class PluginSymbolFacts
{
    /// <summary>
    /// Determines whether a compilation contains a source type marked as a plugin entry point.
    /// </summary>
    /// <param name="rootNamespace">The compilation's global namespace.</param>
    /// <param name="pluginAttributeType">The plugin marker attribute type.</param>
    /// <returns><see langword="true"/> when a source type or nested source type declares the marker; otherwise <see langword="false"/>.</returns>
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

    /// <summary>
    /// Finds the first source location declared for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol whose locations are searched.</param>
    /// <returns>The first source location, or <see langword="null"/> for metadata-only symbols.</returns>
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

    /// <summary>
    /// Determines whether a named type implements a specific interface using Roslyn symbol identity.
    /// </summary>
    /// <param name="type">The named type to inspect.</param>
    /// <param name="interfaceType">The interface to find.</param>
    /// <returns><see langword="true"/> when the interface occurs in the type's complete interface set; otherwise <see langword="false"/>.</returns>
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

    /// <summary>
    /// Determines whether a type is, or implements, a specific interface.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="interfaceType">The interface to match.</param>
    /// <returns><see langword="true"/> for the interface itself or an implementing named type; otherwise <see langword="false"/>.</returns>
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
