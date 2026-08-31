using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Creates the input and output schemas published with MCP tools.
/// </summary>
internal interface IToolSchemaFactory
{
    /// <summary>
    /// Creates the published input schema for a request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <returns>The created input schema.</returns>
    JsonElement CreateInputSchema<TRequest>();

    /// <summary>
    /// Creates the published input schema for a request type selected at runtime.
    /// </summary>
    /// <param name="requestType">The request type to describe.</param>
    /// <returns>The schema published for the request object.</returns>
    JsonElement CreateInputSchemaForType(Type requestType);

    /// <summary>
    /// Creates an output schema for a tool that publishes its response without an envelope.
    /// </summary>
    /// <param name="responseType">The response type to describe.</param>
    /// <returns>The schema for the directly published response.</returns>
    JsonElement CreateDirectOutputSchema(Type responseType);

    /// <summary>
    /// Creates the result-envelope schema appropriate to a query or mutation tool.
    /// </summary>
    /// <param name="kind">The tool category that determines the envelope shape.</param>
    /// <param name="responseType">The successful response payload type.</param>
    /// <returns>The schema for the tool's published result envelope.</returns>
    JsonElement CreateOutputSchema(PublishedToolKind kind, Type responseType);
}
