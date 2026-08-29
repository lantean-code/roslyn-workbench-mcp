using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Protocol.Results;

internal sealed record ToolContinuation
{
    /// <summary>
    /// Gets the Kind.
    /// </summary>
    [Description("Action required before the request can continue.")]
    public ToolContinuationKind Kind { get; }

    /// <summary>
    /// Gets the tool to call, when the continuation requires one.
    /// </summary>
    [Description("Tool to call when kind is CallTool.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tool { get; }

    /// <summary>
    /// Gets the tool choices, when the continuation requires a selection.
    /// </summary>
    [Description("Allowed tool choices when kind is ChooseTool.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Tools { get; }

    /// <summary>
    /// Gets the Instruction.
    /// </summary>
    [Description("Agent-facing instruction explaining how to continue.")]
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
