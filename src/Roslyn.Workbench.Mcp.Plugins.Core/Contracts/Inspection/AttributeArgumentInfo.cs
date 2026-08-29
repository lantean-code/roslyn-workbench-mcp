namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one constructor or named argument on an attribute.
/// </summary>
internal sealed record AttributeArgumentInfo
{
    /// <summary>
    /// Gets the argument name for named arguments.
    /// </summary>
    [Description("The argument name for named arguments.")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets the argument type display name.
    /// </summary>
    [Description("The argument type display name.")]
    public string? Type { get; init; }

    /// <summary>
    /// Gets the argument value.
    /// </summary>
    [Description("The argument value.")]
    public string? Value { get; init; }
}
