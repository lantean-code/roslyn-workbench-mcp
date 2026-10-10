using System.Text.Json;
using System.Text.Json.Nodes;

using ModelContextProtocol.Client;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal static class ToolCatalogueCanonicalizer
{
    private static readonly HashSet<string> _contractFields = new(StringComparer.Ordinal)
    {
        "_meta",
        "annotations",
        "description",
        "execution",
        "icons",
        "inputSchema",
        "name",
        "outputSchema",
        "title",
    };

    public static string Create(IReadOnlyDictionary<string, IList<McpClientTool>> catalogues)
    {
        var canonicalCatalogues = new JsonObject();
        foreach (var (name, tools) in catalogues.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            canonicalCatalogues.Add(name, CreateCatalogue(tools));
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        var json = canonicalCatalogues.ToJsonString(options);
        return json.ReplaceLineEndings("\r\n") + "\r\n";
    }

    internal static JsonNode? CreateContract(JsonObject protocolTool, string toolName)
    {
        var contract = new JsonObject();
        foreach (var (field, value) in protocolTool)
        {
            if (value is null)
            {
                continue;
            }

            if (!_contractFields.Contains(field))
            {
                throw new InvalidOperationException(
                    $"Tool '{toolName}' has unclassified non-null protocol field '{field}'.");
            }

            contract.Add(field, value.DeepClone());
        }

        return Canonicalize(contract);
    }

    private static JsonNode? CreateContract(McpClientTool tool)
    {
        var protocolTool = JsonSerializer.SerializeToNode(tool.ProtocolTool) as JsonObject
            ?? throw new InvalidOperationException($"Tool '{tool.Name}' could not be serialised.");

        return CreateContract(protocolTool, tool.Name);
    }

    private static JsonArray CreateCatalogue(IList<McpClientTool> tools)
    {
        return new JsonArray(
            tools
                .OrderBy(static tool => tool.Name, StringComparer.Ordinal)
                .Select(CreateContract)
                .ToArray());
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
