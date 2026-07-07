namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Represents the published success-shape contract for plugin query tools.
/// </summary>
public abstract record QueryResponse;

/// <summary>
/// Represents a singleton query response.
/// </summary>
/// <typeparam name="TValue">The published value type.</typeparam>
public sealed record QueryResponse<TValue> : QueryResponse
{
    /// <summary>
    /// Gets the published value.
    /// </summary>
    public TValue Value { get; init; } = default!;
}
