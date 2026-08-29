namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one callable parameter.
/// </summary>
internal sealed record ParameterInfo
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    [Description("The parameter name.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parameter type information.
    /// </summary>
    [Description("The parameter type information.")]
    public TypeInfo? Type { get; init; }

    /// <summary>
    /// Gets the parameter passing mode.
    /// </summary>
    [Description("The parameter passing mode.")]
    public string RefKind { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the parameter is optional.
    /// </summary>
    [Description("Whether the parameter is optional.")]
    public bool IsOptional { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter has an explicit default value.
    /// </summary>
    [Description("Whether the parameter has an explicit default value.")]
    public bool HasExplicitDefaultValue { get; init; }

    /// <summary>
    /// Gets the formatted default value, when available.
    /// </summary>
    [Description("The formatted default value, when available.")]
    public string? DefaultValue { get; init; }
}
