using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Retention;

internal sealed class BoundedExpiringStoreUpdateResult<TValue>
    where TValue : class
{
    [MemberNotNullWhen(true, nameof(OriginalValue), nameof(UpdatedValue))]
    public bool WasFound { get; }

    public TValue? OriginalValue { get; }

    public TValue? UpdatedValue { get; }

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

internal static class BoundedExpiringStoreUpdateResult
{
    public static BoundedExpiringStoreUpdateResult<TValue> NotFound<TValue>()
        where TValue : class
    {
        return new BoundedExpiringStoreUpdateResult<TValue>(
            wasFound: false,
            originalValue: null,
            updatedValue: null);
    }

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
