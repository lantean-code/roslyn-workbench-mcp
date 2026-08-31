using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Provides analyzer-focused type projections for Roslyn symbols that represent values.
/// </summary>
internal static class SymbolExtensions
{
    /// <summary>
    /// Gets the declared value type for a local or parameter symbol.
    /// </summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <returns>The local or parameter type, or <see langword="null"/> for other symbol kinds.</returns>
    public static ITypeSymbol? GetSymbolType(this ISymbol symbol)
    {
        return symbol switch
        {
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null,
        };
    }
}
