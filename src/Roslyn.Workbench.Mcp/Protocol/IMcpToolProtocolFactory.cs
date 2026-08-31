namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Creates MCP tool definitions from server, plugin, and Code Action contracts.
/// </summary>
internal interface IMcpToolProtocolFactory
{
    /// <summary>
    /// Creates a definition for a server-owned tool using the standard annotations.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="name">The protocol name used to invoke the tool.</param>
    /// <param name="title">The human-readable tool title.</param>
    /// <param name="description">The guidance published to MCP clients.</param>
    /// <param name="readOnly">Whether the operation is restricted to read-only behaviour.</param>
    /// <param name="destructive">Whether the operation may discard or overwrite data.</param>
    /// <param name="resultSummary">The optional text emitted with a successful structured result.</param>
    /// <param name="outputSchemaMode">The mode that controls publication of output schemas.</param>
    /// <returns>The created server-owned tool.</returns>
    Tool CreateServerOwnedTool<TRequest, TResponse>(
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        string? resultSummary,
        ToolOutputSchemaMode outputSchemaMode)
        where TRequest : class;

    /// <summary>
    /// Creates a definition for a server-owned tool with explicit MCP annotations.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="name">The protocol name used to invoke the tool.</param>
    /// <param name="title">The human-readable tool title.</param>
    /// <param name="description">The guidance published to MCP clients.</param>
    /// <param name="readOnly">Whether the operation is restricted to read-only behaviour.</param>
    /// <param name="destructive">Whether the operation may discard or overwrite data.</param>
    /// <param name="resultSummary">The optional text emitted with a successful structured result.</param>
    /// <param name="outputSchemaMode">The mode that controls publication of output schemas.</param>
    /// <param name="idempotent">Whether repeated invocations with the same input have the same effect.</param>
    /// <param name="openWorld">Whether the tool may interact with resources outside the current workspace.</param>
    /// <returns>The created server-owned tool with annotations.</returns>
    Tool CreateServerOwnedToolWithAnnotations<TRequest, TResponse>(
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        string? resultSummary,
        ToolOutputSchemaMode outputSchemaMode,
        bool idempotent,
        bool openWorld)
        where TRequest : class;

    /// <summary>
    /// Creates an MCP definition for a registered plugin tool.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="tool">The registered plugin tool to publish.</param>
    /// <param name="outputSchemaMode">The mode that controls publication of output schemas.</param>
    /// <returns>The created plugin tool.</returns>
    Tool CreatePluginTool<TRequest>(RegisteredTool tool, ToolOutputSchemaMode outputSchemaMode)
        where TRequest : WorkspaceBoundRequest;

    /// <summary>
    /// Creates an MCP definition for an internal Code Action tool.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="metadata">The published name, title, and description of the tool.</param>
    /// <param name="kind">The Code Action operation represented by the tool.</param>
    /// <param name="responseType">The structured response type published by the tool.</param>
    /// <param name="outputSchemaMode">The mode that controls publication of output schemas.</param>
    /// <returns>The created Code Action tool.</returns>
    Tool CreateCodeActionTool<TRequest>(
        CodeActionToolMetadata metadata,
        CodeActionToolKind kind,
        Type responseType,
        ToolOutputSchemaMode outputSchemaMode)
        where TRequest : WorkspaceBoundRequest;
}
