using System.Text.Json;
using System.Text.Json.Nodes;

using ModelContextProtocol.Client;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal static class ToolCatalogueCanonicalizer
{
    private static readonly string[] _contractFields =
    [
        "name",
        "description",
        "annotations",
        "inputSchema",
        "outputSchema",
    ];

    public static string Create(IList<McpClientTool> tools)
    {
        var canonicalTools = new JsonArray();
        foreach (var tool in tools.OrderBy(static item => item.Name, StringComparer.Ordinal))
        {
            var protocolTool = JsonSerializer.SerializeToNode(tool.ProtocolTool) as JsonObject
                ?? throw new InvalidOperationException($"Tool '{tool.Name}' could not be serialised.");

            var contract = new JsonObject();
            foreach (var field in _contractFields)
            {
                if (protocolTool.TryGetPropertyValue(field, out var value))
                {
                    contract.Add(field, value?.DeepClone());
                }
            }

            canonicalTools.Add(Canonicalize(contract));
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        var json = canonicalTools.ToJsonString(options);
        return json.Replace("\n", "\r\n", StringComparison.Ordinal) + "\r\n";
    }

    private static JsonNode? Canonicalize(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            var result = new JsonObject();
            foreach (var property in jsonObject.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                result.Add(property.Key, Canonicalize(property.Value));
            }

            return result;
        }

        if (node is JsonArray jsonArray)
        {
            var result = new JsonArray();
            foreach (var item in jsonArray)
            {
                result.Add(Canonicalize(item));
            }

            return result;
        }

        return node?.DeepClone();
    }
}
