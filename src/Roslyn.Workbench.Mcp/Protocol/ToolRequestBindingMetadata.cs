namespace Roslyn.Workbench.Mcp.Protocol;

internal sealed class ToolRequestBindingMetadata
{
    public string[] RequiredNames { get; }

    public Dictionary<string, int> RequiredIndexes { get; }

    public EnumArgumentMetadata[] EnumArguments { get; }

    public Dictionary<string, int> EnumIndexes { get; }

    public ToolRequestBindingMetadata(
        string[] requiredNames,
        Dictionary<string, int> requiredIndexes,
        EnumArgumentMetadata[] enumArguments,
        Dictionary<string, int> enumIndexes)
    {
        RequiredNames = requiredNames;
        RequiredIndexes = requiredIndexes;
        EnumArguments = enumArguments;
        EnumIndexes = enumIndexes;
    }
}
