using System.Runtime.CompilerServices;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection.Caching;

internal sealed class ReferenceDiscoveryCacheKey : IEquatable<ReferenceDiscoveryCacheKey>
{
    private readonly Guid[] _documentIds;
    private readonly Solution _solution;
    private readonly ISymbol _symbol;

    public ReferenceDiscoveryCacheKey(Solution solution, ISymbol symbol, IReadOnlyList<Document> documents)
    {
        _solution = solution;
        _symbol = symbol;
        _documentIds = new Guid[documents.Count];

        for (var index = 0; index < documents.Count; index++)
        {
            _documentIds[index] = documents[index].Id.Id;
        }

        Array.Sort(_documentIds);
    }

    public bool Equals(ReferenceDiscoveryCacheKey? other)
    {
        return other is not null
            && ReferenceEquals(_solution, other._solution)
            && SymbolEqualityComparer.Default.Equals(_symbol, other._symbol)
            && _documentIds.AsSpan().SequenceEqual(other._documentIds);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ReferenceDiscoveryCacheKey);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(RuntimeHelpers.GetHashCode(_solution));
        hashCode.Add(_symbol, SymbolEqualityComparer.Default);

        foreach (var documentId in _documentIds)
        {
            hashCode.Add(documentId);
        }

        return hashCode.ToHashCode();
    }
}
