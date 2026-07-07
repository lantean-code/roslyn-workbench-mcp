using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

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
