using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

internal sealed class McpToolProtocolFactory : IMcpToolProtocolFactory
{
    private readonly IToolSchemaFactory _schemaFactory;

    public McpToolProtocolFactory(IToolSchemaFactory schemaFactory)
    {
        _schemaFactory = schemaFactory;
    }

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
