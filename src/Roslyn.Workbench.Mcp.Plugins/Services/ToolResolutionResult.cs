using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Services;

#pragma warning disable CA1000 // Resolution factories belong with the generic result contract and encode its valid states.
/// <summary>
/// Represents a successful resolved Roslyn value or a normalized rejection for a tool helper operation.
/// </summary>
/// <typeparam name="TValue">The resolved Roslyn value type.</typeparam>
/// <typeparam name="TResponse">The tool response payload type.</typeparam>
public sealed record ToolResolutionResult<TValue, TResponse>
    where TValue : class
{
    /// <summary>
    /// Gets the rejection result, when resolution failed.
    /// </summary>
    public PluginExecutionResult<TResponse>? Rejection { get; }

    /// <summary>
    /// Gets the resolved value, when resolution succeeded.
    /// </summary>
    public TValue? Value { get; }

    /// <summary>
    /// Gets a value indicating whether resolution failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Rejection))]
    [MemberNotNullWhen(false, nameof(Value))]
    public bool HasRejection => Rejection is not null;

    private ToolResolutionResult(
        PluginExecutionResult<TResponse>? rejection,
        TValue? value)
    {
        Rejection = rejection;
        Value = value;
    }

    /// <summary>
    /// Creates a successful resolution.
    /// </summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>The successful resolution result.</returns>
    public static ToolResolutionResult<TValue, TResponse> Resolved(TValue value)
    {
        return new ToolResolutionResult<TValue, TResponse>(rejection: null, value);
    }

    /// <summary>
    /// Creates a rejected resolution.
    /// </summary>
    /// <param name="rejection">The normalized rejection.</param>
    /// <returns>The rejected resolution result.</returns>
    public static ToolResolutionResult<TValue, TResponse> Rejected(
        PluginExecutionResult<TResponse> rejection)
    {
        return new ToolResolutionResult<TValue, TResponse>(rejection, value: null);
    }
}
#pragma warning restore CA1000
