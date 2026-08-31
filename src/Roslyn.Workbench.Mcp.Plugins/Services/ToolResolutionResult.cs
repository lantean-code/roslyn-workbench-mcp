using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Services;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolResolutionResult{TValue, TResponse}"/> class.
    /// </summary>
    /// <param name="rejection">The normalized rejection when resolution failed.</param>
    /// <param name="value">The resolved value when resolution succeeded.</param>
    internal ToolResolutionResult(
        PluginExecutionResult<TResponse>? rejection,
        TValue? value)
    {
        if (rejection is null)
        {
            ArgumentNullException.ThrowIfNull(value);
        }

        Rejection = rejection;
        Value = value;
    }
}

/// <summary>
/// Creates tool resolution results.
/// </summary>
public static class ToolResolutionResult
{
    /// <summary>
    /// Creates a successful resolution.
    /// </summary>
    /// <typeparam name="TValue">The resolved Roslyn value type.</typeparam>
    /// <typeparam name="TResponse">The tool response payload type.</typeparam>
    /// <param name="value">The resolved value.</param>
    /// <returns>The successful resolution result.</returns>
    public static ToolResolutionResult<TValue, TResponse> Resolved<TValue, TResponse>(TValue value)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(value);

        return new ToolResolutionResult<TValue, TResponse>(rejection: null, value);
    }

    /// <summary>
    /// Creates a rejected resolution.
    /// </summary>
    /// <typeparam name="TValue">The resolved Roslyn value type.</typeparam>
    /// <typeparam name="TResponse">The tool response payload type.</typeparam>
    /// <param name="rejection">The normalized rejection.</param>
    /// <returns>The rejected resolution result.</returns>
    public static ToolResolutionResult<TValue, TResponse> Rejected<TValue, TResponse>(
        PluginExecutionResult<TResponse> rejection)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(rejection);

        return new ToolResolutionResult<TValue, TResponse>(rejection, value: null);
    }
}
