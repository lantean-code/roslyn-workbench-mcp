namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-api-surface.
/// </summary>
public sealed record ApiSurfaceData
{
    /// <summary>
    /// Gets the returned exported API symbols.
    /// </summary>
    public BoundedCollection<ApiSymbolInfo> Symbols { get; init; } = BoundedCollection<ApiSymbolInfo>.Empty();
}
