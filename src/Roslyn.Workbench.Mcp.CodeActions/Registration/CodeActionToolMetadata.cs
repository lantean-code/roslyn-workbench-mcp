namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Describes how a Code Action tool is published to MCP clients.
/// </summary>
internal sealed record CodeActionToolMetadata
{
    /// <summary>
    /// Gets the stable MCP tool name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable tool title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the description presented to MCP clients.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional summary used to describe successful results.
    /// </summary>
    public string? ResultSummary { get; init; }

    /// <summary>
    /// Gets the MCP behavior hints for the tool.
    /// </summary>
    public CodeActionToolBehavior Behavior { get; init; } = new();
}
