using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

internal interface IToolSchemaFactory
{
    JsonElement CreateInputSchema<TRequest>();

    JsonElement CreateInputSchemaForType(Type requestType);

    JsonElement CreateDirectOutputSchema(Type responseType);

    JsonElement CreateOutputSchema(PublishedToolKind kind, Type responseType);
}
