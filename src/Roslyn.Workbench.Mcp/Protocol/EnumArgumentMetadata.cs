namespace Roslyn.Workbench.Mcp.Protocol;

internal sealed class EnumArgumentMetadata
{
    public string Name { get; }

    public Type Type { get; }

    public Func<object, object?> Getter { get; }

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
