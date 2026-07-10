namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one projected attribute on a symbol.
/// </summary>
public sealed record AttributeInfo
{
    /// <summary>
    /// Gets the attribute display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the attribute type information.
    /// </summary>
    public TypeInfo? Type { get; init; }

    /// <summary>
    /// Gets a value indicating whether the attribute was inherited from a base type.
    /// </summary>
    public bool Inherited { get; init; }

    /// <summary>
    /// Gets the positional constructor arguments.
    /// </summary>
    public IReadOnlyList<AttributeArgumentInfo> ConstructorArguments { get; init; } = [];

    /// <summary>
    /// Gets the named arguments.
    /// </summary>
    public IReadOnlyList<AttributeArgumentInfo> NamedArguments { get; init; } = [];
}
