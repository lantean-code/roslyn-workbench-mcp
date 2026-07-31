namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Marks a dedicated immutable semantic key that can identify a plugin query result.
/// </summary>
/// <remarks>
/// Implementations must be named immutable reference types with stable value equality and hash-code behaviour.
/// </remarks>
#pragma warning disable CA1040 // This marker constrains plugin-authored cache keys so the analyzer can enforce immutable value semantics.
public interface IQueryResultCacheKey
{
}
#pragma warning restore CA1040
