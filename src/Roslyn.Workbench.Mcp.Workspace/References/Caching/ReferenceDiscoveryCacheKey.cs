using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.References.Caching;

/// <summary>
/// Identifies a reference search by symbol and selected document scope.
/// </summary>
internal sealed class ReferenceDiscoveryCacheKey : IWorkspaceQueryCacheKey, IEquatable<ReferenceDiscoveryCacheKey>
{
    private readonly Guid[] _documentIds;
    private readonly ISymbol _symbol;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceDiscoveryCacheKey"/> class.
    /// </summary>
    /// <param name="symbol">The symbol represented by the reference-discovery cache key.</param>
    /// <param name="documents">The documents included in the selected scope or cache identity.</param>
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

    /// <summary>
    /// Determines whether this value equals the supplied value.
    /// </summary>
    /// <param name="other">The value to compare with this instance.</param>
    /// <returns><see langword="true"/> when both keys identify the same symbol and document scope; otherwise, <see langword="false"/>.</returns>
    public bool Equals(ReferenceDiscoveryCacheKey? other)
    {
        return other is not null
            && SymbolEqualityComparer.Default.Equals(_symbol, other._symbol)
            && _documentIds.AsSpan().SequenceEqual(other._documentIds);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return Equals(obj as ReferenceDiscoveryCacheKey);
    }

    /// <inheritdoc/>
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
