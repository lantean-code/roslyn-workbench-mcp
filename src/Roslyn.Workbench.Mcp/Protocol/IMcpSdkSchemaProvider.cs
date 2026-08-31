using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Produces JSON schemas using the MCP SDK's serialization configuration.
/// </summary>
internal interface IMcpSdkSchemaProvider
{
    /// <summary>
    /// Produces the input schema for a request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <returns>The input schema.</returns>
    JsonElement GetInputSchema<TRequest>();

    /// <summary>
    /// Produces the input schema for a request type selected at runtime.
    /// </summary>
    /// <param name="requestType">The request type whose schema or metadata is required.</param>
    /// <returns>The schema published for the request object.</returns>
    JsonElement GetInputSchemaForType(Type requestType);

    /// <summary>
    /// Produces the JSON schema for a value type.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns>The value schema.</returns>
    JsonElement GetValueSchema<TValue>();

    /// <summary>
    /// Produces the JSON schema for a value type selected at runtime.
    /// </summary>
    /// <param name="valueType">The value type to describe.</param>
    /// <returns>The JSON schema for values of the specified type.</returns>
    JsonElement GetValueSchema(Type valueType);
}
