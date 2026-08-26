using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class InputSchemaExporter
{
    private const string RequestPointer = "#/properties/request";
    private const string DefinitionsPointer = "#/$defs";

    public static JsonElement ExtractRequestSchema(JsonElement root)
    {
        var request = root.GetProperty("properties").GetProperty("request");
        var schema = ParseObject(request);

        NormalizeObjectType(schema);
        CloseEmptyObject(schema);
        CopyDefinitions(root, schema);
        RebaseLocalReferences(schema);
        ValidateLocalReferences(schema);

        return JsonSerializer.SerializeToElement(schema);
    }

    private static void CloseEmptyObject(JsonObject schema)
    {
        if (schema["type"] is not JsonValue typeNode
            || !typeNode.TryGetValue<string>(out var type)
            || !string.Equals(type, "object", StringComparison.Ordinal))
        {
            return;
        }

        if (schema.ContainsKey("additionalProperties"))
        {
            return;
        }

        if (schema["properties"] is JsonObject properties && properties.Count > 0)
        {
            return;
        }

        schema["additionalProperties"] = false;
    }

    private static void NormalizeObjectType(JsonObject schema)
    {
        if (schema["type"] is not JsonArray types)
        {
            return;
        }

        foreach (var type in types)
        {
            if (string.Equals(type?.GetValue<string>(), "object", StringComparison.Ordinal))
            {
                schema["type"] = "object";
                return;
            }
        }
    }

    private static void CopyDefinitions(JsonElement root, JsonObject schema)
    {
        if (!root.TryGetProperty("$defs", out var definitions))
        {
            return;
        }

        schema["$defs"] = JsonNode.Parse(definitions.GetRawText());
    }

    private static void RebaseLocalReferences(JsonNode? node)
    {
        if (node is JsonObject schemaObject)
        {
            RebaseObjectReference(schemaObject);

            foreach (var property in schemaObject)
            {
                RebaseLocalReferences(property.Value);
            }

            return;
        }

        if (node is not JsonArray schemaArray)
        {
            return;
        }

        foreach (var item in schemaArray)
        {
            RebaseLocalReferences(item);
        }
    }

    private static void RebaseObjectReference(JsonObject schema)
    {
        if (schema["$ref"] is not JsonValue referenceValue
            || !referenceValue.TryGetValue<string>(out var reference))
        {
            return;
        }

        if (string.Equals(reference, RequestPointer, StringComparison.Ordinal))
        {
            schema["$ref"] = "#";
            return;
        }

        if (reference.StartsWith(RequestPointer + "/", StringComparison.Ordinal))
        {
            schema["$ref"] = "#" + reference[RequestPointer.Length..];
            return;
        }

        if (IsDefinitionsReference(reference) || !IsJsonPointerReference(reference))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Generated input schema reference '{reference}' escapes the extracted request schema.");
    }

    private static void ValidateLocalReferences(JsonObject schema)
    {
        ValidateLocalReferences(schema, schema);
    }

    private static void ValidateLocalReferences(JsonNode? node, JsonObject root)
    {
        if (node is JsonObject schemaObject)
        {
            ValidateObjectReference(schemaObject, root);

            foreach (var property in schemaObject)
            {
                ValidateLocalReferences(property.Value, root);
            }

            return;
        }

        if (node is not JsonArray schemaArray)
        {
            return;
        }

        foreach (var item in schemaArray)
        {
            ValidateLocalReferences(item, root);
        }
    }

    private static void ValidateObjectReference(JsonObject schema, JsonObject root)
    {
        if (schema["$ref"] is not JsonValue referenceValue
            || !referenceValue.TryGetValue<string>(out var reference)
            || !IsJsonPointerReference(reference))
        {
            return;
        }

        if (!TryResolvePointer(root, reference))
        {
            throw new InvalidOperationException(
                $"Generated input schema contains unresolved local reference '{reference}' after extracting the request schema.");
        }
    }

    private static bool TryResolvePointer(JsonNode root, string reference)
    {
        if (string.Equals(reference, "#", StringComparison.Ordinal))
        {
            return true;
        }

        var pointer = Uri.UnescapeDataString(reference[1..]);
        var tokens = pointer[1..].Split('/');
        JsonNode? current = root;

        foreach (var encodedToken in tokens)
        {
            if (!TryDecodeToken(encodedToken, out var token))
            {
                return false;
            }

            if (current is JsonObject currentObject)
            {
                if (!currentObject.TryGetPropertyValue(token, out current))
                {
                    return false;
                }
            }
            else
            {
                if (current is not JsonArray currentArray
                    || !int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                    || token.Length > 1 && token[0] == '0'
                    || index >= currentArray.Count)
                {
                    return false;
                }

                current = currentArray[index];
            }
        }

        return true;
    }

    private static bool TryDecodeToken(string encodedToken, out string token)
    {
        var decoded = new StringBuilder(encodedToken.Length);
        for (var index = 0; index < encodedToken.Length; index++)
        {
            var character = encodedToken[index];
            if (character != '~')
            {
                decoded.Append(character);
                continue;
            }

            if (index + 1 >= encodedToken.Length)
            {
                token = string.Empty;
                return false;
            }

            var escape = encodedToken[++index];
            if (escape == '0')
            {
                decoded.Append('~');
            }
            else if (escape == '1')
            {
                decoded.Append('/');
            }
            else
            {
                token = string.Empty;
                return false;
            }
        }

        token = decoded.ToString();
        return true;
    }

    private static bool IsDefinitionsReference(string reference)
    {
        return string.Equals(reference, DefinitionsPointer, StringComparison.Ordinal)
            || reference.StartsWith(DefinitionsPointer + "/", StringComparison.Ordinal);
    }

    private static bool IsJsonPointerReference(string reference)
    {
        return string.Equals(reference, "#", StringComparison.Ordinal)
            || reference.StartsWith("#/", StringComparison.Ordinal);
    }

    private static JsonObject ParseObject(JsonElement element)
    {
        var node = JsonNode.Parse(element.GetRawText());
        if (node is not JsonObject schema)
        {
            throw new InvalidOperationException("Generated request schema was not a JSON object.");
        }

        return schema;
    }
}
