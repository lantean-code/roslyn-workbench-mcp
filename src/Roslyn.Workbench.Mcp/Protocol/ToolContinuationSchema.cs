using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class ToolContinuationSchema
{
    public static JsonElement Create()
    {
        var alternatives = new JsonArray
        {
            CreateCallToolSchema(),
            CreateChooseToolSchema(),
            CreateInstructionOnlySchema(ToolContinuationKind.RetryRequest),
            CreateInstructionOnlySchema(ToolContinuationKind.ReviseRequest),
            CreateInstructionOnlySchema(ToolContinuationKind.ResolveExternally),
        };

        var schema = new JsonObject
        {
            ["oneOf"] = alternatives,
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonObject CreateCallToolSchema()
    {
        var properties = CreateCommonProperties(ToolContinuationKind.CallTool);
        properties["tool"] = CreateNonEmptyStringSchema();

        return CreateObjectSchema(properties, ["kind", "tool", "instruction"]);
    }

    private static JsonObject CreateChooseToolSchema()
    {
        var toolsSchema = new JsonObject
        {
            ["type"] = "array",
            ["minItems"] = 1,
            ["uniqueItems"] = true,
            ["items"] = CreateNonEmptyStringSchema(),
        };

        var properties = CreateCommonProperties(ToolContinuationKind.ChooseTool);
        properties["tools"] = toolsSchema;

        return CreateObjectSchema(properties, ["kind", "tools", "instruction"]);
    }

    private static JsonObject CreateInstructionOnlySchema(ToolContinuationKind kind)
    {
        var properties = CreateCommonProperties(kind);
        return CreateObjectSchema(properties, ["kind", "instruction"]);
    }

    private static JsonObject CreateCommonProperties(ToolContinuationKind kind)
    {
        return new JsonObject
        {
            ["kind"] = new JsonObject
            {
                ["const"] = kind.ToString(),
            },
            ["instruction"] = CreateNonEmptyStringSchema(),
        };
    }

    private static JsonObject CreateNonEmptyStringSchema()
    {
        return new JsonObject
        {
            ["type"] = "string",
            ["minLength"] = 1,
        };
    }

    private static JsonObject CreateObjectSchema(JsonObject properties, JsonArray required)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = required,
            ["properties"] = properties,
        };
    }
}
