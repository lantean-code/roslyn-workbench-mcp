using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Reuses repeated request objects through local definitions without changing their expanded contracts.
/// </summary>
internal static class InputSchemaCompactor
{
    private const int _maximumExpandedSchemas = 4096;

    private static readonly HashSet<string> _scopeKeywords = new(StringComparer.Ordinal)
    {
        "$defs", "$id", "$anchor", "$dynamicAnchor", "$dynamicRef", "$recursiveAnchor", "$recursiveRef",
    };

    /// <summary>
    /// Compacts an acyclic SDK request schema only when local object reuse reduces its serialised size.
    /// </summary>
    /// <param name="schema">The self-contained request schema produced by the SDK exporter.</param>
    /// <returns>A smaller equivalent schema, or the original when reuse is unhelpful or unsupported.</returns>
    public static JsonElement Compact(JsonElement schema)
    {
        if (JsonNode.Parse(schema.GetRawText()) is not JsonObject root)
        {
            return schema;
        }

        var activeReferences = new HashSet<string>(StringComparer.Ordinal);
        var remainingSchemas = _maximumExpandedSchemas;
        if (!TryExpand(root, root, activeReferences, ref remainingSchemas, out var expanded))
        {
            return schema;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var hints = new Dictionary<string, string>(StringComparer.Ordinal);
        CountObjectShapes(expanded, counts, hints, isRoot: true);
        var candidates = counts
            .Where(static entry => entry.Value > 1)
            .Select(static entry => entry.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var selected = new Dictionary<string, string>(StringComparer.Ordinal);
        var smallest = root;
        var smallestSize = GetSize(root);
        while (TrySelectDefinition(expanded, candidates, hints, selected, smallestSize, out var shape, out var compacted))
        {
            selected.Add(shape, CreateDefinitionName(hints[shape], selected));
            smallest = compacted;
            smallestSize = GetSize(smallest);
        }

        return selected.Count == 0 ? schema : JsonSerializer.SerializeToElement(smallest);
    }

    private static bool TryExpand(JsonObject schema, JsonObject root, HashSet<string> activeReferences, ref int remainingSchemas, out JsonObject expanded)
    {
        expanded = schema;
        if (--remainingSchemas < 0
            || schema.Any(static property => _scopeKeywords.Contains(property.Key))
            || schema.TryGetPropertyValue("$schema", out var dialect) && dialect?.ToString() != "https://json-schema.org/draft/2020-12/schema")
        {
            return false;
        }

        if (schema.ContainsKey("$ref"))
        {
            return TryExpandReference(schema, root, activeReferences, ref remainingSchemas, out expanded);
        }

        expanded = (JsonObject)schema.DeepClone();
        foreach (var child in GetChildSchemas(expanded).ToArray())
        {
            if (!TryExpand(child, root, activeReferences, ref remainingSchemas, out var expandedChild))
            {
                return false;
            }

            child.ReplaceWith(expandedChild);
        }

        return true;
    }

    private static bool TryExpandReference(JsonObject schema, JsonObject root, HashSet<string> activeReferences, ref int remainingSchemas, out JsonObject expanded)
    {
        expanded = schema;
        if (schema.Any(static property => property.Key is not "$ref" and not "description")
            || schema["$ref"] is not JsonValue referenceValue
            || !referenceValue.TryGetValue<string>(out var reference)
            || !TryResolveReference(root, reference, out var target)
            || !activeReferences.Add(reference))
        {
            return false;
        }

        var succeeded = TryExpand(target, root, activeReferences, ref remainingSchemas, out expanded);
        activeReferences.Remove(reference);
        if (succeeded && schema.TryGetPropertyValue("description", out var description))
        {
            expanded["description"] = description?.DeepClone();
        }

        return succeeded;
    }

    private static bool TryResolveReference(JsonObject root, string reference, out JsonObject target)
    {
        target = root;
        if (reference == "#")
        {
            return true;
        }

        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            return false;
        }

        var pointer = Uri.UnescapeDataString(reference[2..]);
        foreach (var token in pointer.Split('/'))
        {
            var name = token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (target[name] is not JsonObject child)
            {
                return false;
            }

            target = child;
        }

        return true;
    }

    private static void CountObjectShapes(JsonObject schema, Dictionary<string, int> counts, Dictionary<string, string> hints, bool isRoot)
    {
        if (!isRoot && schema["properties"] is JsonObject)
        {
            var key = GetShapeKey(schema);
            counts.TryGetValue(key, out var count);
            counts[key] = count + 1;
            hints.TryAdd(key, GetDefinitionHint(schema));
        }

        foreach (var child in GetChildSchemas(schema))
        {
            CountObjectShapes(child, counts, hints, isRoot: false);
        }
    }

    private static string GetDefinitionHint(JsonObject schema)
    {
        JsonNode node = schema;
        while (node.Parent is { } parent)
        {
            if (parent is JsonObject map && map.Parent is JsonObject owner && ReferenceEquals(owner["properties"], map))
            {
                var propertyName = node.GetPropertyName();
                return propertyName.Length == 0 ? "object" : propertyName;
            }

            node = parent;
        }

        return "object";
    }

    private static string CreateDefinitionName(string hint, Dictionary<string, string> selected)
    {
        var name = hint;
        var suffix = 2;
        while (selected.ContainsValue(name))
        {
            name = $"{hint}{suffix++}";
        }

        return name;
    }

    private static bool TrySelectDefinition(
        JsonObject expanded,
        IReadOnlyList<string> candidates,
        Dictionary<string, string> hints,
        Dictionary<string, string> selected,
        int currentSize,
        out string shape,
        out JsonObject compacted)
    {
        shape = string.Empty;
        compacted = expanded;
        var smallestSize = currentSize;
        foreach (var candidate in candidates)
        {
            if (selected.ContainsKey(candidate))
            {
                continue;
            }

            var proposed = new Dictionary<string, string>(selected, StringComparer.Ordinal)
            {
                [candidate] = CreateDefinitionName(hints[candidate], selected),
            };

            var definitions = new JsonObject();
            var result = CreateCompactedSchema(expanded, proposed, definitions, skipReference: false);
            result["$defs"] = definitions;
            var size = GetSize(result);
            if (size < smallestSize)
            {
                smallestSize = size;
                shape = candidate;
                compacted = result;
            }
        }

        return shape.Length > 0;
    }

    private static JsonObject CreateCompactedSchema(JsonObject schema, IReadOnlyDictionary<string, string> selected, JsonObject definitions, bool skipReference)
    {
        if (!skipReference && schema["properties"] is JsonObject && selected.TryGetValue(GetShapeKey(schema), out var name))
        {
            if (!definitions.ContainsKey(name))
            {
                var body = (JsonObject)schema.DeepClone();
                body.Remove("description");
                var definition = CreateCompactedSchema(body, selected, definitions, skipReference: true);
                definitions[name] = definition;
            }

            var pointerToken = name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
            var reference = new JsonObject
            {
                ["$ref"] = $"#/$defs/{Uri.EscapeDataString(pointerToken)}",
            };

            if (schema.TryGetPropertyValue("description", out var description))
            {
                reference["description"] = description?.DeepClone();
            }

            return reference;
        }

        var result = (JsonObject)schema.DeepClone();
        foreach (var child in GetChildSchemas(result).ToArray())
        {
            var compactedChild = CreateCompactedSchema(child, selected, definitions, skipReference: false);
            child.ReplaceWith(compactedChild);
        }

        return result;
    }

    private static string GetShapeKey(JsonObject schema)
    {
        var body = (JsonObject)schema.DeepClone();
        body.Remove("description");
        return body.ToJsonString();
    }

    private static int GetSize(JsonObject schema)
    {
        return Encoding.UTF8.GetByteCount(schema.ToJsonString());
    }

    private static IEnumerable<JsonObject> GetChildSchemas(JsonObject schema)
    {
        foreach (var (keyword, value) in schema)
        {
            if (keyword is "properties" or "patternProperties" or "dependentSchemas" && value is JsonObject map)
            {
                foreach (var child in map.Select(static entry => entry.Value).OfType<JsonObject>())
                {
                    yield return child;
                }
            }
            else if (keyword is "allOf" or "anyOf" or "oneOf" or "prefixItems" && value is JsonArray array)
            {
                foreach (var child in array.OfType<JsonObject>())
                {
                    yield return child;
                }
            }
            else if (keyword is "items" or "contains" or "additionalProperties" or "unevaluatedProperties" or "unevaluatedItems" or "propertyNames" or "not" or "if" or "then" or "else" && value is JsonObject child)
            {
                yield return child;
            }
        }
    }
}
