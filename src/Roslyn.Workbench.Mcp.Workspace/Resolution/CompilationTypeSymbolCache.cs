namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

internal sealed class CompilationTypeSymbolCache
{
    private readonly Dictionary<Compilation, Dictionary<string, INamedTypeSymbol?>> _symbolsByCompilation = new(ReferenceEqualityComparer.Instance);

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
