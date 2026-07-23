namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one constructor or named argument on an attribute.
/// </summary>
internal sealed record AttributeArgumentInfo
{
    /// <summary>
    /// Gets the argument name for named arguments.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the argument type display name.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Gets the argument value.
    /// </summary>
    public string? Value { get; init; }
}
