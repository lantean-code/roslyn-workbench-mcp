using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.Protocol;

internal sealed class ValidationArgumentMetadata
{
    public string Name { get; }

    public Func<object, object?> Getter { get; }

    public ValidationAttribute[] Attributes { get; }

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
