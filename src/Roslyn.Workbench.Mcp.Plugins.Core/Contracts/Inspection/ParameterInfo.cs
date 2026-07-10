namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one callable parameter.
/// </summary>
public sealed record ParameterInfo
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parameter type information.
    /// </summary>
    public TypeInfo? Type { get; init; }

    /// <summary>
    /// Gets the parameter passing mode.
    /// </summary>
    public string RefKind { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the parameter is optional.
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter has an explicit default value.
    /// </summary>
    public bool HasExplicitDefaultValue { get; init; }

    /// <summary>
    /// Gets the formatted default value, when available.
    /// </summary>
    public string? DefaultValue { get; init; }
}
