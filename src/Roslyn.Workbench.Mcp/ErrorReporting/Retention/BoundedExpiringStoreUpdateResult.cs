using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Retention;

/// <summary>
/// Reports whether a stored value was updated and exposes its value before and after the update.
/// </summary>
/// <typeparam name="TValue">The type of value retained by the store.</typeparam>
internal sealed class BoundedExpiringStoreUpdateResult<TValue>
    where TValue : class
{
    /// <summary>
    /// Gets a value indicating whether the store contained an unexpired value for the requested key.
    /// </summary>
    [MemberNotNullWhen(true, nameof(OriginalValue), nameof(UpdatedValue))]
    public bool WasFound { get; }

    /// <summary>
    /// Gets the value held before the update, or <see langword="null"/> when no value was found.
    /// </summary>
    public TValue? OriginalValue { get; }

    /// <summary>
    /// Gets the replacement value, or <see langword="null"/> when no value was found.
    /// </summary>
    public TValue? UpdatedValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundedExpiringStoreUpdateResult{TValue}"/> class.
    /// </summary>
    /// <param name="wasFound">Whether the store contained a value for the requested key.</param>
    /// <param name="originalValue">The value stored before the successful update.</param>
    /// <param name="updatedValue">The replacement value stored by the successful update.</param>
    internal BoundedExpiringStoreUpdateResult(
        bool wasFound,
        TValue? originalValue,
        TValue? updatedValue)
    {
        WasFound = wasFound;
        OriginalValue = originalValue;
        UpdatedValue = updatedValue;
    }
}

/// <summary>
/// Creates results for bounded expiring store update operations.
/// </summary>
internal static class BoundedExpiringStoreUpdateResult
{
    /// <summary>
    /// Creates a result that represents a missing value.
    /// </summary>
    /// <typeparam name="TValue">The type of value retained by the store.</typeparam>
    /// <returns>A result that represents a missing value.</returns>
    public static BoundedExpiringStoreUpdateResult<TValue> NotFound<TValue>()
        where TValue : class
    {
        return new BoundedExpiringStoreUpdateResult<TValue>(
            wasFound: false,
            originalValue: null,
            updatedValue: null);
    }

    /// <summary>
    /// Creates a successful update result containing the original and replacement values.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="originalValue">The value stored before the update.</param>
    /// <param name="updatedValue">The replacement value stored by the update.</param>
    /// <returns>A result that records both versions of the updated value.</returns>
    public static BoundedExpiringStoreUpdateResult<TValue> Updated<TValue>(
        TValue originalValue,
        TValue updatedValue)
        where TValue : class
    {
        return new BoundedExpiringStoreUpdateResult<TValue>(
            wasFound: true,
            originalValue,
            updatedValue);
    }
}
