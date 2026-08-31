using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

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
    public SelectorResolveStatus Status { get; }

    /// <summary>
    /// Gets the resolved value when <see cref="Status"/> is <see cref="SelectorResolveStatus.Resolved"/>.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets a value indicating whether resolution succeeded with a value.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsResolved => Status == SelectorResolveStatus.Resolved && Value is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectorResolveResult{T}"/> class.
    /// </summary>
    /// <param name="status">The resolution outcome.</param>
    /// <param name="value">The resolved value, required when <paramref name="status"/> is <see cref="SelectorResolveStatus.Resolved"/>.</param>
    internal SelectorResolveResult(SelectorResolveStatus status, T? value)
    {
        if (status == SelectorResolveStatus.Resolved)
        {
            ArgumentNullException.ThrowIfNull(value);
        }

        Status = status;
        Value = value;
    }
}

/// <summary>
/// Creates selector resolution results.
/// </summary>
public static class SelectorResolveResult
{
    /// <summary>
    /// Creates a resolved outcome.
    /// </summary>
    /// <typeparam name="T">The resolved value type.</typeparam>
    /// <param name="value">The resolved value.</param>
    /// <returns>The resolution result.</returns>
    public static SelectorResolveResult<T> Resolved<T>(T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);

        return new SelectorResolveResult<T>(SelectorResolveStatus.Resolved, value);
    }

    /// <summary>
    /// Creates a not-found outcome.
    /// </summary>
    /// <typeparam name="T">The resolved value type.</typeparam>
    /// <returns>The resolution result.</returns>
    public static SelectorResolveResult<T> NotFound<T>()
        where T : class
    {
        return new SelectorResolveResult<T>(SelectorResolveStatus.NotFound, value: null);
    }

    /// <summary>
    /// Creates an ambiguous outcome.
    /// </summary>
    /// <typeparam name="T">The resolved value type.</typeparam>
    /// <returns>The resolution result.</returns>
    public static SelectorResolveResult<T> Ambiguous<T>()
        where T : class
    {
        return new SelectorResolveResult<T>(SelectorResolveStatus.Ambiguous, value: null);
    }

    /// <summary>
    /// Creates an invalid-selector outcome.
    /// </summary>
    /// <typeparam name="T">The resolved value type.</typeparam>
    /// <returns>The resolution result.</returns>
    public static SelectorResolveResult<T> Invalid<T>()
        where T : class
    {
        return new SelectorResolveResult<T>(SelectorResolveStatus.Invalid, value: null);
    }
}
