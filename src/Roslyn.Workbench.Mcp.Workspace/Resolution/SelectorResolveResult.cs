using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

#pragma warning disable CA1000 // Resolution factories belong with the generic result contract and encode its valid states.
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
    /// Gets a value indicating whether resolution succeeded with a value.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsResolved => Status == SelectorResolveStatus.Resolved && Value is not null;

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
#pragma warning restore CA1000
