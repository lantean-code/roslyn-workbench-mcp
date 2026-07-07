namespace Roslyn.Workbench.Mcp.Contracts.Results;

/// <summary>
/// Declares the collection property that is published as the top-level <c>items</c> array for a query response.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class PublishedCollectionResponseAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PublishedCollectionResponseAttribute"/> class.
    /// </summary>
    /// <param name="collectionPropertyName">The CLR property name that supplies the published collection items.</param>
    public PublishedCollectionResponseAttribute(string collectionPropertyName)
    {
        CollectionPropertyName = collectionPropertyName;
    }

    /// <summary>
    /// Gets the CLR property name that supplies the published collection items.
    /// </summary>
    public string CollectionPropertyName { get; }

    /// <summary>
    /// Gets or sets the CLR property name that supplies truncation details, when present.
    /// </summary>
    public string? TruncationPropertyName { get; init; }
}
