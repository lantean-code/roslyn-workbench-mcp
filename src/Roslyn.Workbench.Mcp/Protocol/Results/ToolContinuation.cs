using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Protocol.Results;

internal sealed record ToolContinuation
{
    public ToolContinuationKind Kind { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tool { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Tools { get; }

    public string Instruction { get; }

    private ToolContinuation(
        ToolContinuationKind kind,
        string? tool,
        IReadOnlyList<string>? tools,
        string instruction)
    {
        Kind = kind;
        Tool = tool;
        Tools = tools;
        Instruction = instruction;
    }

    public static ToolContinuation CallTool(string tool, string instruction)
    {
        return new ToolContinuation(ToolContinuationKind.CallTool, tool, null, instruction);
    }

    public static ToolContinuation ChooseTool(IReadOnlyList<string> tools, string instruction)
    {
        return new ToolContinuation(ToolContinuationKind.ChooseTool, null, tools, instruction);
    }

    public static ToolContinuation RetryRequest(string instruction)
    {
        return new ToolContinuation(ToolContinuationKind.RetryRequest, null, null, instruction);
    }

    public static ToolContinuation ReviseRequest(string instruction)
    {
        return new ToolContinuation(ToolContinuationKind.ReviseRequest, null, null, instruction);
    }

    public static ToolContinuation ResolveExternally(string instruction)
    {
        return new ToolContinuation(ToolContinuationKind.ResolveExternally, null, null, instruction);
    }
}
