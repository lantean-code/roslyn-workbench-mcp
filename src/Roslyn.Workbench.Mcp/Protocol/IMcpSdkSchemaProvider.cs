using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

internal interface IMcpSdkSchemaProvider
{
    JsonElement GetInputSchema<TRequest>();

    JsonElement GetInputSchemaForType(Type requestType);

    JsonElement GetValueSchema<TValue>();

    JsonElement GetValueSchema(Type valueType);
}
