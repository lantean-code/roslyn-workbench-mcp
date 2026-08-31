namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

/// <summary>
/// Caches metadata-name lookups independently for each Roslyn compilation.
/// </summary>
internal sealed class CompilationTypeSymbolCache
{
    private readonly Dictionary<Compilation, Dictionary<string, INamedTypeSymbol?>> _symbolsByCompilation = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Gets a named type from a compilation, reusing an earlier lookup when available.
    /// </summary>
    /// <param name="compilation">The compilation in which to locate the metadata type.</param>
    /// <param name="metadataName">The fully qualified metadata name of the type to locate.</param>
    /// <returns>The matching type symbol, or <see langword="null"/> when the metadata name is unresolved.</returns>
    public INamedTypeSymbol? GetTypeByMetadataName(Compilation compilation, string metadataName)
    {
        if (!_symbolsByCompilation.TryGetValue(compilation, out var symbolsByMetadataName))
        {
            symbolsByMetadataName = new Dictionary<string, INamedTypeSymbol?>(StringComparer.Ordinal);
            _symbolsByCompilation.Add(compilation, symbolsByMetadataName);
        }

        if (!symbolsByMetadataName.TryGetValue(metadataName, out var symbol))
        {
            symbol = compilation.GetTypeByMetadataName(metadataName);
            symbolsByMetadataName.Add(metadataName, symbol);
        }

        return symbol;
    }
}
