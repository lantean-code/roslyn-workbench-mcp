using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Assembles published MCP tool definitions from contract schemas and catalogue metadata.
/// </summary>
internal sealed class McpToolProtocolFactory : IMcpToolProtocolFactory
{
    private readonly IToolSchemaFactory _schemaFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolProtocolFactory"/> class.
    /// </summary>
    /// <param name="schemaFactory">The factory that supplies published input and output schemas.</param>
    public McpToolProtocolFactory(IToolSchemaFactory schemaFactory)
    {
        _schemaFactory = schemaFactory;
    }

    /// <summary>
    /// Creates a server-owned tool definition using annotations inferred from its behaviour.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="name">The protocol name used to invoke the tool.</param>
    /// <param name="title">The human-readable tool title.</param>
    /// <param name="description">The guidance published to MCP clients.</param>
    /// <param name="readOnly">Whether the operation is restricted to read-only behaviour.</param>
    /// <param name="destructive">Whether the operation may discard or overwrite data.</param>
    /// <param name="resultSummary">Optional guidance describing the structured result.</param>
    /// <param name="outputSchemaMode">The mode that controls publication of output schemas.</param>
    /// <returns>The created server-owned tool.</returns>
    public Tool CreateServerOwnedTool<TRequest, TResponse>(
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        string? resultSummary,
        ToolOutputSchemaMode outputSchemaMode)
        where TRequest : class
    {
        return CreateServerOwnedToolWithAnnotations<TRequest, TResponse>(
            name,
            title,
            description,
            readOnly,
            destructive,
            resultSummary,
            outputSchemaMode,
            idempotent: readOnly,
            openWorld: false);
    }

    /// <summary>
    /// Creates a server-owned tool definition with explicit MCP behaviour annotations.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="name">The protocol name used to invoke the tool.</param>
    /// <param name="title">The human-readable tool title.</param>
    /// <param name="description">The guidance published to MCP clients.</param>
    /// <param name="readOnly">Whether the operation is restricted to read-only behaviour.</param>
    /// <param name="destructive">Whether the operation may discard or overwrite data.</param>
    /// <param name="resultSummary">Optional guidance describing the structured result.</param>
    /// <param name="outputSchemaMode">The mode that controls publication of output schemas.</param>
    /// <param name="idempotent">Whether repeated invocations with the same input have the same effect.</param>
    /// <param name="openWorld">Whether the tool may interact with resources outside the current workspace.</param>
    /// <returns>The created server-owned tool with annotations.</returns>
    public Tool CreateServerOwnedToolWithAnnotations<TRequest, TResponse>(
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        string? resultSummary,
        ToolOutputSchemaMode outputSchemaMode,
        bool idempotent,
        bool openWorld)
        where TRequest : class
    {
        var inputSchema = _schemaFactory.CreateInputSchema<TRequest>();
        var publishedDescription = CreatePublishedDescription(description, inputSchema, resultSummary);
        var annotations = new ToolAnnotations
        {
            Title = title,
            ReadOnlyHint = readOnly,
            IdempotentHint = idempotent,
            OpenWorldHint = openWorld,
            DestructiveHint = destructive,
        };

        return new Tool
        {
            Name = name,
            Title = title,
            Description = publishedDescription,
            InputSchema = inputSchema,
            OutputSchema = outputSchemaMode == ToolOutputSchemaMode.Full
                ? _schemaFactory.CreateDirectOutputSchema(typeof(TResponse))
                : null,
            Annotations = annotations,
        };
    }

    /// <summary>
    /// Creates an MCP definition for a registered plugin tool.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="tool">The registered plugin tool to publish.</param>
    /// <param name="outputSchemaMode">The mode that controls publication of output schemas.</param>
    /// <returns>The created plugin tool.</returns>
    public Tool CreatePluginTool<TRequest>(RegisteredTool tool, ToolOutputSchemaMode outputSchemaMode)
        where TRequest : WorkspaceBoundRequest
    {
        return CreateCatalogueTool<TRequest>(
            tool.Metadata.Name,
            tool.Metadata.Title,
            tool.Metadata.Description,
            tool.Metadata.ResultSummary,
            tool.Kind == ToolKind.Query ? PublishedToolKind.Query : PublishedToolKind.Mutation,
            tool.ResponseType,
            destructive: tool.Metadata.Behavior.Destructive,
            idempotent: tool.Kind == ToolKind.Query,
            outputSchemaMode: outputSchemaMode);
    }

    /// <summary>
    /// Creates an MCP definition for an internal Code Action tool.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="metadata">The published identity and behaviour of the tool.</param>
    /// <param name="kind">The Code Action operation represented by the tool.</param>
    /// <param name="responseType">The successful response payload type.</param>
    /// <param name="outputSchemaMode">The mode that controls publication of output schemas.</param>
    /// <returns>The created Code Action tool.</returns>
    public Tool CreateCodeActionTool<TRequest>(
        CodeActionToolMetadata metadata,
        CodeActionToolKind kind,
        Type responseType,
        ToolOutputSchemaMode outputSchemaMode)
        where TRequest : WorkspaceBoundRequest
    {
        return CreateCatalogueTool<TRequest>(
            metadata.Name,
            metadata.Title,
            metadata.Description,
            metadata.ResultSummary,
            kind == CodeActionToolKind.Query ? PublishedToolKind.Query : PublishedToolKind.Mutation,
            responseType,
            destructive: metadata.Behavior.Destructive,
            idempotent: metadata.Behavior.Idempotent,
            outputSchemaMode: outputSchemaMode);
    }

    private Tool CreateCatalogueTool<TRequest>(
        string name,
        string title,
        string description,
        string? resultSummary,
        PublishedToolKind kind,
        Type responseType,
        bool destructive,
        bool idempotent,
        ToolOutputSchemaMode outputSchemaMode)
        where TRequest : WorkspaceBoundRequest
    {
        var inputSchema = _schemaFactory.CreateInputSchema<TRequest>();
        var publishedDescription = CreatePublishedDescription(description, inputSchema, resultSummary);
        var readOnly = kind == PublishedToolKind.Query;
        var annotations = new ToolAnnotations
        {
            Title = title,
            ReadOnlyHint = readOnly,
            IdempotentHint = idempotent,
            OpenWorldHint = false,
            DestructiveHint = kind == PublishedToolKind.Mutation && destructive,
        };

        return new Tool
        {
            Name = name,
            Title = title,
            Description = publishedDescription,
            InputSchema = inputSchema,
            OutputSchema = outputSchemaMode == ToolOutputSchemaMode.Full
                ? _schemaFactory.CreateOutputSchema(kind, responseType)
                : null,
            Annotations = annotations,
        };
    }

    private static string CreatePublishedDescription(string description, JsonElement inputSchema, string? resultSummary)
    {
        var publishedDescription = description;
        if (inputSchema.TryGetProperty("description", out var inputDescription))
        {
            var inputGuidance = inputDescription.GetString();
            if (!string.IsNullOrWhiteSpace(inputGuidance))
            {
                publishedDescription = $"{publishedDescription} Input: {inputGuidance}";
            }
        }

        if (!string.IsNullOrWhiteSpace(resultSummary))
        {
            publishedDescription = $"{publishedDescription} Result: {resultSummary}";
        }

        return publishedDescription;
    }
}
