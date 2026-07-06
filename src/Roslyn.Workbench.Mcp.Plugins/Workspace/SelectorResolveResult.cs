namespace Roslyn.Workbench.Mcp.Plugins.Workspace;

/// <summary>
/// Represents the outcome of resolving a selector against the current workspace snapshot.
/// </summary>
/// <typeparam name="T">The resolved value type.</typeparam>
public sealed record SelectorResolveResult<T>
    where T : class
{
    /// <summary>
    /// Gets the resolution status.
    /// </summary>
    public SelectorResolveStatus Status { get; init; }

    /// <summary>
    /// Gets the resolved value when <see cref="Status"/> is <see cref="SelectorResolveStatus.Resolved"/>.
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// Creates a resolved outcome.
    /// </summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>The resolution result.</returns>
    public static SelectorResolveResult<T> Resolved(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new SelectorResolveResult<T>
        {
            Status = SelectorResolveStatus.Resolved,
            Value = value,
        };
    }

    /// <summary>
    /// Creates a not-found outcome.
    /// </summary>
    /// <returns>The resolution result.</returns>
    public static SelectorResolveResult<T> NotFound()
    {
        return new SelectorResolveResult<T>
        {
            Status = SelectorResolveStatus.NotFound,
        };
    }

    /// <summary>
    /// Creates an ambiguous outcome.
    /// </summary>
    /// <returns>The resolution result.</returns>
    public static SelectorResolveResult<T> Ambiguous()
    {
        return new SelectorResolveResult<T>
        {
            Status = SelectorResolveStatus.Ambiguous,
        };
    }
}
