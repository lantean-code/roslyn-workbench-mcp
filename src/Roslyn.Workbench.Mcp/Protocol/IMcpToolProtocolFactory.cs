namespace Roslyn.Workbench.Mcp.Protocol;

internal interface IMcpToolProtocolFactory
{
    Tool CreateServerOwnedTool<TRequest, TResponse>(
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        string? resultSummary,
        ToolOutputSchemaMode outputSchemaMode)
        where TRequest : class;

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

    Tool CreatePluginTool<TRequest>(RegisteredTool tool, ToolOutputSchemaMode outputSchemaMode)
        where TRequest : WorkspaceBoundRequest;

    Tool CreateCodeActionTool<TRequest>(
        CodeActionToolMetadata metadata,
        CodeActionToolKind kind,
        Type responseType,
        ToolOutputSchemaMode outputSchemaMode)
        where TRequest : WorkspaceBoundRequest;
}
