namespace Roslyn.Workbench.Mcp.Protocol;

internal static class McpToolProtocolFactory
{
    public static Tool CreatePluginTool<TRequest>(
        RegisteredTool tool,
        ToolOutputSchemaMode outputSchemaMode)
        where TRequest : WorkspaceBoundRequest
    {
        return Create<TRequest>(
            tool.Metadata.Name,
            tool.Metadata.Title,
            tool.Metadata.Description,
            tool.Metadata.ResultSummary,
            tool.Kind == ToolKind.Query ? PublishedToolKind.Query : PublishedToolKind.Mutation,
            tool.ResponseType,
            tool.Metadata.Behavior.Destructive,
            outputSchemaMode);
    }

    public static Tool CreateCodeActionTool<TRequest>(
        CodeActionToolMetadata metadata,
        CodeActionToolKind kind,
        Type responseType,
        ToolOutputSchemaMode outputSchemaMode)
        where TRequest : WorkspaceBoundRequest
    {
        return Create<TRequest>(
            metadata.Name,
            metadata.Title,
            metadata.Description,
            metadata.ResultSummary,
            kind == CodeActionToolKind.Query ? PublishedToolKind.Query : PublishedToolKind.Mutation,
            responseType,
            metadata.Behavior.Destructive,
            outputSchemaMode);
    }

    private static Tool Create<TRequest>(
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
        var publishedDescription = string.IsNullOrWhiteSpace(resultSummary)
            ? description
            : $"{description} Result: {resultSummary}";
        return new Tool
        {
            Name = name,
            Title = title,
            Description = publishedDescription,
            InputSchema = ToolSchemaFactory.CreateInputSchema<TRequest>(),
            OutputSchema = outputSchemaMode == ToolOutputSchemaMode.Full
                ? ToolSchemaFactory.CreateOutputSchema(kind, responseType)
                : null,
            Annotations = new ToolAnnotations
            {
                Title = title,
                ReadOnlyHint = kind == PublishedToolKind.Query,
                IdempotentHint = kind == PublishedToolKind.Query,
                OpenWorldHint = false,
                DestructiveHint = kind == PublishedToolKind.Mutation && destructive,
            },
        };
    }
}
