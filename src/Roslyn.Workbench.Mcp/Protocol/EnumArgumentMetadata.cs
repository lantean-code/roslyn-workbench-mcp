namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Describes an enum-valued property that requires validation after request deserialization.
/// </summary>
internal sealed class EnumArgumentMetadata
{
    /// <summary>
    /// Gets the request property name reported when validation fails.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the enum type accepted by the property.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the accessor used to read the property from a request instance.
    /// </summary>
    public Func<object, object?> Getter { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnumArgumentMetadata"/> class.
    /// </summary>
    /// <param name="name">The name used to identify the resulting item.</param>
    /// <param name="type">The runtime type of the bound enum argument.</param>
    /// <param name="getter">The compiled accessor used to read the argument value.</param>
    public EnumArgumentMetadata(
        string name,
        Type type,
        Func<object, object?> getter)
    {
        Name = name;
        Type = type;
        Getter = getter;
    }
}
