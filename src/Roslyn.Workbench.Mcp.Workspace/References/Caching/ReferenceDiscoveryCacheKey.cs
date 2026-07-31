using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.References.Caching;

internal sealed class ReferenceDiscoveryCacheKey : IWorkspaceQueryCacheKey, IEquatable<ReferenceDiscoveryCacheKey>
{
    private readonly Guid[] _documentIds;
    private readonly ISymbol _symbol;

    public ReferenceDiscoveryCacheKey(ISymbol symbol, IReadOnlyList<Document> documents)
    {
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
        hashCode.Add(_symbol, SymbolEqualityComparer.Default);

        foreach (var documentId in _documentIds)
        {
            hashCode.Add(documentId);
        }

        return hashCode.ToHashCode();
    }
}
