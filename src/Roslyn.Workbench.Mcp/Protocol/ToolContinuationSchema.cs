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
        properties["tool"] = CreateNonEmptyStringSchema("Tool to call before continuing the original request.");

        return CreateObjectSchema(properties, ["kind", "tool", "instruction"]);
    }

    private static JsonObject CreateChooseToolSchema()
    {
        var toolsSchema = new JsonObject
        {
            ["type"] = "array",
            ["description"] = "Allowed tool choices from which the agent must select one.",
            ["minItems"] = 1,
            ["uniqueItems"] = true,
            ["items"] = CreateNonEmptyStringSchema("Name of a tool the agent may choose."),
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
                ["description"] = "Action required before the original request can continue.",
            },
            ["instruction"] = CreateNonEmptyStringSchema("Agent-facing instruction explaining how to continue."),
        };
    }

    private static JsonObject CreateNonEmptyStringSchema(string description)
    {
        return new JsonObject
        {
            ["type"] = "string",
            ["description"] = description,
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
