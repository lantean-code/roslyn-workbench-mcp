namespace Roslyn.Workbench.Mcp.Protocol;

internal sealed class McpToolProtocolFactory : IMcpToolProtocolFactory
{
    private readonly ToolSchemaFactory _schemaFactory;

    public McpToolProtocolFactory(ToolSchemaFactory schemaFactory)
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
        var publishedDescription = CreatePublishedDescription(description, resultSummary);
        var annotations = new ToolAnnotations
        {
            Title = title,
            ReadOnlyHint = readOnly,
            IdempotentHint = readOnly,
            OpenWorldHint = false,
            DestructiveHint = destructive,
        };

        return new Tool
        {
            Name = name,
            Title = title,
            Description = publishedDescription,
            InputSchema = _schemaFactory.CreateInputSchema<TRequest>(),
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
            tool.Metadata.Behavior.Destructive,
            outputSchemaMode);
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
            metadata.Behavior.Destructive,
            outputSchemaMode);
    }

    private Tool CreateCatalogueTool<TRequest>(
        string name,
        string title,
        string description,
        string? resultSummary,
        PublishedToolKind kind,
        Type responseType,
        bool destructive,
        ToolOutputSchemaMode outputSchemaMode)
        where TRequest : WorkspaceBoundRequest
    {
        var publishedDescription = CreatePublishedDescription(description, resultSummary);
        var readOnly = kind == PublishedToolKind.Query;
        var annotations = new ToolAnnotations
        {
            Title = title,
            ReadOnlyHint = readOnly,
            IdempotentHint = readOnly,
            OpenWorldHint = false,
            DestructiveHint = kind == PublishedToolKind.Mutation && destructive,
        };

        return new Tool
        {
            Name = name,
            Title = title,
            Description = publishedDescription,
            InputSchema = _schemaFactory.CreateInputSchema<TRequest>(),
            OutputSchema = outputSchemaMode == ToolOutputSchemaMode.Full
                ? _schemaFactory.CreateOutputSchema(kind, responseType)
                : null,
            Annotations = annotations,
        };
    }

    private static string CreatePublishedDescription(string description, string? resultSummary)
    {
        return string.IsNullOrWhiteSpace(resultSummary)
            ? description
            : $"{description} Result: {resultSummary}";
    }
}
