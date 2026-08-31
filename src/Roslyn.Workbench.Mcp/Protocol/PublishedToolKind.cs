namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Defines the supported published tool kind values.
/// </summary>
internal enum PublishedToolKind
{
    /// <summary>
    /// A read operation whose result uses the query envelope.
    /// </summary>
    Query,
    /// <summary>
    /// A transactional change whose result uses the mutation envelope.
    /// </summary>
    Mutation,
}
