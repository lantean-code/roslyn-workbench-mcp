using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins;

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
    public PluginExecutionResult<TResponse>? Rejection { get; init; }

    /// <summary>
    /// Gets the resolved value, when resolution succeeded.
    /// </summary>
    public TValue? Value { get; init; }

    /// <summary>
    /// Gets a value indicating whether resolution failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Rejection))]
    [MemberNotNullWhen(false, nameof(Value))]
    public bool HasRejection => Rejection is not null;
}
