using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Describes a request property whose data-annotation rules are validated during binding.
/// </summary>
internal sealed class ValidationArgumentMetadata
{
    /// <summary>
    /// Gets the request property name reported when validation fails.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the accessor used to read the property from a request instance.
    /// </summary>
    public Func<object, object?> Getter { get; }

    /// <summary>
    /// Gets the validation rules applied to the property.
    /// </summary>
    public ValidationAttribute[] Attributes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationArgumentMetadata"/> class.
    /// </summary>
    /// <param name="name">The request property name.</param>
    /// <param name="getter">The compiled accessor used to read the argument value.</param>
    /// <param name="attributes">The validation attributes applied to the argument.</param>
    public ValidationArgumentMetadata(
        string name,
        Func<object, object?> getter,
        ValidationAttribute[] attributes)
    {
        Name = name;
        Getter = getter;
        Attributes = attributes;
    }
}
