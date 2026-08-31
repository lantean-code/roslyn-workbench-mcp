using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Protocol.Results;

/// <summary>
/// Describes the action an agent must take before continuing a failed or incomplete request.
/// </summary>
internal sealed record ToolContinuation
{
    /// <summary>
    /// Action required before the request can continue.
    /// </summary>
    [Description("Action required before the request can continue.")]
    public ToolContinuationKind Kind { get; }

    /// <summary>
    /// Tool to call when kind is CallTool.
    /// </summary>
    [Description("Tool to call when kind is CallTool.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tool { get; }

    /// <summary>
    /// Allowed tool choices when kind is ChooseTool.
    /// </summary>
    [Description("Allowed tool choices when kind is ChooseTool.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Tools { get; }

    /// <summary>
    /// Agent-facing instruction explaining how to continue.
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

    /// <summary>
    /// Creates a continuation that directs the client to call a specific tool.
    /// </summary>
    /// <param name="tool">The published name of the tool to call.</param>
    /// <param name="instruction">The instruction that tells the client how to continue.</param>
    /// <returns>The tool continuation.</returns>
    public static ToolContinuation CallTool(string tool, string instruction)
    {
        return new ToolContinuation(ToolContinuationKind.CallTool, tool, null, instruction);
    }

    /// <summary>
    /// Creates a continuation that asks the client to choose a tool.
    /// </summary>
    /// <param name="tools">The published tool names from which the client may choose.</param>
    /// <param name="instruction">The instruction that tells the client how to continue.</param>
    /// <returns>The tool continuation.</returns>
    public static ToolContinuation ChooseTool(IReadOnlyList<string> tools, string instruction)
    {
        return new ToolContinuation(ToolContinuationKind.ChooseTool, null, tools, instruction);
    }

    /// <summary>
    /// Creates a continuation that asks the client to retry the request.
    /// </summary>
    /// <param name="instruction">The instruction that tells the client how to continue.</param>
    /// <returns>The tool continuation.</returns>
    public static ToolContinuation RetryRequest(string instruction)
    {
        return new ToolContinuation(ToolContinuationKind.RetryRequest, null, null, instruction);
    }

    /// <summary>
    /// Creates a continuation that asks the client to revise the request.
    /// </summary>
    /// <param name="instruction">The instruction that tells the client how to continue.</param>
    /// <returns>The tool continuation.</returns>
    public static ToolContinuation ReviseRequest(string instruction)
    {
        return new ToolContinuation(ToolContinuationKind.ReviseRequest, null, null, instruction);
    }

    /// <summary>
    /// Creates a continuation for recovery that must be completed outside the MCP tool workflow.
    /// </summary>
    /// <param name="instruction">The instruction that tells the client how to continue.</param>
    /// <returns>A continuation containing the external recovery instruction.</returns>
    public static ToolContinuation ResolveExternally(string instruction)
    {
        return new ToolContinuation(ToolContinuationKind.ResolveExternally, null, null, instruction);
    }
}
