using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Writes deterministic human-readable and machine-readable tool-reference artifacts.
/// </summary>
internal static class ToolReferenceWriter
{
    private const string _catalogSchemaId = "tool-catalog.schema.json";
    private const string _documentationBaseUrl = "https://lantean-code.github.io/roslyn-workbench-mcp/";
    private const string _detailSchemaId = "tool-detail.schema.json";
    private const string _developmentSourceTag = "0.0.0-dev";
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    /// <summary>
    /// Writes the complete reference artifact set.
    /// </summary>
    /// <param name="outputDirectory">The directory that receives generated files.</param>
    /// <param name="identity">The compiled Host build identity.</param>
    /// <param name="formatVersion">The machine-readable reference format identifier.</param>
    /// <param name="entries">The production tools to document.</param>
    public static void Write(
        string outputDirectory,
        ToolReferenceBuildIdentity identity,
        string formatVersion,
        IReadOnlyList<ToolReferenceEntry> entries)
    {
        var dataDirectory = Path.Combine(outputDirectory, "data");
        var schemaDirectory = Path.Combine(outputDirectory, "schemas");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(schemaDirectory);

        WriteJson(Path.Combine(outputDirectory, "catalog.json"), CreateCatalog(identity, formatVersion, entries));
        WriteJson(Path.Combine(schemaDirectory, "tool-catalog.schema.json"), CreateCatalogSchema());
        WriteJson(Path.Combine(schemaDirectory, "tool-detail.schema.json"), CreateDetailSchema());
        WriteText(Path.Combine(outputDirectory, "index.md"), CreateIndex(identity, entries));

        foreach (var entry in entries)
        {
            var detail = CreateDetail(identity, formatVersion, entry);
            WriteJson(Path.Combine(dataDirectory, $"{entry.Name}.json"), detail);
            WriteText(Path.Combine(outputDirectory, $"{entry.Name}.md"), CreateToolPage(identity, entry));
        }
    }

    private static JsonObject CreateCatalog(
        ToolReferenceBuildIdentity identity,
        string formatVersion,
        IReadOnlyList<ToolReferenceEntry> entries)
    {
        var tools = new JsonArray();
        foreach (var entry in entries)
        {
            tools.Add(new JsonObject
            {
                ["name"] = entry.Name,
                ["title"] = entry.Title,
                ["area"] = entry.Area,
                ["category"] = entry.Category,
                ["operationKind"] = entry.OperationKind,
                ["summary"] = entry.Summary,
                ["availability"] = entry.Availability,
                ["detailUrl"] = $"data/{entry.Name}.json",
                ["documentationUrl"] = CreateDocumentationUrl(identity.SourceTag, entry.Name),
            });
        }

        return new JsonObject
        {
            ["$schema"] = "schemas/tool-catalog.schema.json",
            ["formatVersion"] = formatVersion,
            ["productVersion"] = identity.ProductVersion,
            ["sourceTag"] = identity.SourceTag,
            ["commit"] = identity.Commit,
            ["tools"] = tools,
        };
    }

    private static JsonObject CreateDetail(
        ToolReferenceBuildIdentity identity,
        string formatVersion,
        ToolReferenceEntry entry)
    {
        var examples = new JsonArray();
        foreach (var example in entry.Examples)
        {
            examples.Add(new JsonObject
            {
                ["workflowId"] = example.WorkflowId,
                ["workflowTitle"] = example.WorkflowTitle,
                ["step"] = example.Step,
                ["id"] = example.Id,
                ["title"] = example.Title,
                ["purpose"] = example.Purpose,
                ["expectedOutcome"] = example.ExpectedOutcome,
                ["representativeResponse"] = example.RepresentativeResponse?.DeepClone(),
                ["request"] = example.Request.DeepClone(),
            });
        }

        return new JsonObject
        {
            ["$schema"] = "../schemas/tool-detail.schema.json",
            ["formatVersion"] = formatVersion,
            ["productVersion"] = identity.ProductVersion,
            ["sourceTag"] = identity.SourceTag,
            ["commit"] = identity.Commit,
            ["name"] = entry.Name,
            ["title"] = entry.Title,
            ["area"] = entry.Area,
            ["category"] = entry.Category,
            ["operationKind"] = entry.OperationKind,
            ["summary"] = entry.Summary,
            ["availability"] = entry.Availability,
            ["documentationUrl"] = CreateDocumentationUrl(identity.SourceTag, entry.Name),
            ["tool"] = entry.ProtocolTool.DeepClone(),
            ["examples"] = examples,
        };
    }

    private static string CreateIndex(
        ToolReferenceBuildIdentity identity,
        IReadOnlyList<ToolReferenceEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Tool reference");
        builder.AppendLine();
        builder.AppendLine($"This reference is generated from the production Host composition for `{identity.ProductVersion}`. The [catalog](catalog.json) is the compact machine-readable index; each tool page links to its complete MCP definition.");

        foreach (var category in entries.GroupBy(static entry => entry.Category).OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            builder.AppendLine();
            builder.AppendLine($"## {category.Key}");
            builder.AppendLine();
            builder.AppendLine("| Tool | Operation | Area | Purpose |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var entry in category)
            {
                builder.AppendLine($"| [`{entry.Name}`]({entry.Name}.md) | {entry.OperationKind} | {entry.Area} | {EscapeTable(entry.Summary)} |");
            }
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private static string CreateToolPage(
        ToolReferenceBuildIdentity identity,
        ToolReferenceEntry entry)
    {
        var builder = new StringBuilder();
        var inputSchema = GetRequiredObject(entry.ProtocolTool, "inputSchema", entry.Name);
        var outputSchema = GetRequiredObject(entry.ProtocolTool, "outputSchema", entry.Name);

        builder.AppendLine($"# {entry.Title}");
        builder.AppendLine();
        builder.AppendLine($"`{entry.Name}` · {entry.OperationKind} · {entry.Area} · {entry.Category}");
        builder.AppendLine();
        builder.AppendLine(entry.Summary);
        builder.AppendLine();
        builder.AppendLine($"**Availability:** {entry.Availability}");
        builder.AppendLine();
        builder.AppendLine($"Generated from Roslyn Workbench MCP `{identity.ProductVersion}` (`{identity.SourceTag}`).");

        AppendAnnotations(builder, entry.ProtocolTool["annotations"] as JsonObject);
        AppendPropertyTable(builder, "Request", inputSchema);
        AppendConstraints(builder, inputSchema);
        AppendPropertyTable(builder, "Response", outputSchema);
        AppendBoundedCollections(builder, outputSchema);
        AppendOutcomes(builder, outputSchema);
        AppendContinuations(builder, outputSchema);
        AppendExamples(builder, entry.Examples);
        AppendSchema(builder, "Complete input schema", inputSchema);
        AppendSchema(builder, "Complete output schema", outputSchema);

        builder.AppendLine();
        builder.AppendLine("## Machine reference");
        builder.AppendLine();
        builder.AppendLine($"- [Complete tool definition](data/{entry.Name}.json)");
        builder.AppendLine("- [Tool catalog](catalog.json)");
        builder.AppendLine("- [Tool detail schema](schemas/tool-detail.schema.json)");
        builder.AppendLine();
        return builder.ToString();
    }

    private static void AppendAnnotations(StringBuilder builder, JsonObject? annotations)
    {
        builder.AppendLine();
        builder.AppendLine("## Behaviour annotations");
        builder.AppendLine();
        if (annotations is null || annotations.Count == 0)
        {
            builder.AppendLine("The tool does not publish behaviour annotations.");
            return;
        }

        builder.AppendLine("| Annotation | Value |");
        builder.AppendLine("| --- | --- |");
        foreach (var annotation in annotations.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"| `{annotation.Key}` | `{annotation.Value?.ToJsonString() ?? "null"}` |");
        }
    }

    private static void AppendPropertyTable(StringBuilder builder, string heading, JsonObject schema)
    {
        builder.AppendLine();
        builder.AppendLine($"## {heading}");
        builder.AppendLine();
        var properties = schema["properties"] as JsonObject;
        if (properties is not null && properties.Count > 0)
        {
            AppendProperties(builder, schema, properties);
            return;
        }

        if (schema["oneOf"] is JsonArray variants)
        {
            foreach (var variantNode in variants)
            {
                if (variantNode is not JsonObject variant
                    || variant["properties"] is not JsonObject variantProperties)
                {
                    continue;
                }

                var label = DescribeEnvelopeVariant(variantProperties);
                builder.AppendLine($"### {label}");
                builder.AppendLine();
                AppendProperties(builder, variant, variantProperties);
                builder.AppendLine();
            }

            return;
        }

        builder.AppendLine("This object has no properties.");
    }

    private static void AppendProperties(StringBuilder builder, JsonObject schema, JsonObject properties)
    {
        var required = (schema["required"] as JsonArray)?
            .Select(static node => node?.GetValue<string>())
            .Where(static value => value is not null)
            .ToHashSet(StringComparer.Ordinal) ?? [];

        builder.AppendLine("| Property | Type | Required | Description |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var property in properties)
        {
            var propertySchema = property.Value as JsonObject;
            var type = DescribeType(propertySchema);
            var isRequired = required.Contains(property.Key) ? "Yes" : "No";
            var description = propertySchema?["description"]?.GetValue<string>() ?? string.Empty;
            var encodedType = HtmlEncoder.Default.Encode(type).Replace("|", "&#124;", StringComparison.Ordinal);
            builder.AppendLine($"| `{property.Key}` | <code>{encodedType}</code> | {isRequired} | {EscapeTable(description)} |");
        }
    }

    private static void AppendConstraints(StringBuilder builder, JsonObject schema)
    {
        var constraints = new List<string>();
        CollectConstraints(schema, "$", constraints);
        if (constraints.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("### Request constraints");
        builder.AppendLine();
        foreach (var constraint in constraints)
        {
            builder.AppendLine($"- {constraint}");
        }
    }

    private static void AppendBoundedCollections(StringBuilder builder, JsonObject schema)
    {
        var bounded = new List<string>();
        CollectBoundedCollections(schema, "$", bounded);
        if (bounded.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("### Bounded collections and continuation");
        builder.AppendLine();
        builder.AppendLine("These response locations publish returned items with explicit bounds and continuation state:");
        builder.AppendLine();
        foreach (var location in bounded.Distinct(StringComparer.Ordinal))
        {
            builder.AppendLine($"- `{location}`");
        }
    }

    private static void AppendContinuations(StringBuilder builder, JsonObject schema)
    {
        var locations = new List<string>();
        CollectPropertyLocations(schema, "$", "continuation", locations);
        if (locations.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("### Continuations and required actions");
        builder.AppendLine();
        builder.AppendLine("A non-success response may include a structured continuation at the following schema location. Follow its tool call or user-action instruction instead of guessing how to retry:");
        builder.AppendLine();
        foreach (var location in locations.Distinct(StringComparer.Ordinal))
        {
            builder.AppendLine($"- `{location}`");
        }
    }

    private static void AppendOutcomes(StringBuilder builder, JsonObject schema)
    {
        var outcomes = new SortedSet<string>(StringComparer.Ordinal);
        CollectNamedValues(schema, "outcome", outcomes);
        if (outcomes.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("### Outcomes");
        builder.AppendLine();
        builder.AppendLine(string.Join(", ", outcomes.Select(static outcome => $"`{outcome}`")));
    }

    private static void AppendExamples(StringBuilder builder, IReadOnlyList<ToolReferenceExample> examples)
    {
        if (examples.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("## Examples");
        foreach (var example in examples)
        {
            builder.AppendLine();
            builder.AppendLine($"### {example.Title}");
            builder.AppendLine();
            builder.AppendLine($"Workflow: **{example.WorkflowTitle}**, step {example.Step}.");
            builder.AppendLine();
            builder.AppendLine(example.Purpose);
            builder.AppendLine();
            builder.AppendLine($"Expected outcome: {example.ExpectedOutcome}");
            builder.AppendLine();
            builder.AppendLine("```json");
            builder.AppendLine(example.Request.ToJsonString(_jsonOptions));
            builder.AppendLine("```");
            if (example.RepresentativeResponse is not null)
            {
                builder.AppendLine();
                builder.AppendLine("Representative response fragment:");
                builder.AppendLine();
                builder.AppendLine("```json");
                builder.AppendLine(example.RepresentativeResponse.ToJsonString(_jsonOptions));
                builder.AppendLine("```");
            }
        }
    }

    private static void AppendSchema(StringBuilder builder, string title, JsonObject schema)
    {
        builder.AppendLine();
        builder.AppendLine("<details>");
        builder.AppendLine($"<summary>{title}</summary>");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(schema.ToJsonString(_jsonOptions));
        builder.AppendLine("```");
        builder.AppendLine("</details>");
    }

    private static void CollectConstraints(JsonNode? node, string location, ICollection<string> constraints)
    {
        if (node is JsonObject value)
        {
            foreach (var keyword in new[] { "oneOf", "anyOf", "allOf", "if", "then", "else", "not" })
            {
                if (value.ContainsKey(keyword))
                {
                    constraints.Add($"`{location}` uses `{keyword}` validation.");
                }
            }

            foreach (var property in value)
            {
                CollectConstraints(property.Value, $"{location}/{EscapePointer(property.Key)}", constraints);
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                CollectConstraints(array[index], $"{location}/{index}", constraints);
            }
        }
    }

    private static void CollectBoundedCollections(JsonNode? node, string location, ICollection<string> locations)
    {
        if (node is JsonObject value)
        {
            if (value["properties"] is JsonObject properties
                && properties.ContainsKey("items")
                && properties.ContainsKey("hasMore"))
            {
                locations.Add(location);
            }

            foreach (var property in value)
            {
                CollectBoundedCollections(property.Value, $"{location}/{EscapePointer(property.Key)}", locations);
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                CollectBoundedCollections(array[index], $"{location}/{index}", locations);
            }
        }
    }

    private static void CollectNamedValues(JsonNode? node, string propertyName, ISet<string> values)
    {
        if (node is JsonObject value)
        {
            if (value["properties"] is JsonObject properties
                && properties[propertyName] is JsonObject propertySchema)
            {
                if (propertySchema["const"] is JsonValue constant
                    && constant.TryGetValue<string>(out var constantValue))
                {
                    values.Add(constantValue);
                }

                if (propertySchema["enum"] is JsonArray options)
                {
                    foreach (var option in options)
                    {
                        if (option is JsonValue optionValue
                            && optionValue.TryGetValue<string>(out var text))
                        {
                            values.Add(text);
                        }
                    }
                }
            }

            foreach (var property in value)
            {
                CollectNamedValues(property.Value, propertyName, values);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                CollectNamedValues(item, propertyName, values);
            }
        }
    }

    private static void CollectPropertyLocations(
        JsonNode? node,
        string location,
        string propertyName,
        ICollection<string> locations)
    {
        if (node is JsonObject value)
        {
            if (value["properties"] is JsonObject properties
                && properties.ContainsKey(propertyName))
            {
                locations.Add($"{location}/properties/{propertyName}");
            }

            foreach (var property in value)
            {
                CollectPropertyLocations(property.Value, $"{location}/{EscapePointer(property.Key)}", propertyName, locations);
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                CollectPropertyLocations(array[index], $"{location}/{index}", propertyName, locations);
            }
        }
    }

    private static string DescribeEnvelopeVariant(JsonObject properties)
    {
        if (properties["ok"] is JsonObject okSchema
            && okSchema["const"] is JsonValue constant
            && constant.TryGetValue<bool>(out var succeeded))
        {
            return succeeded ? "Success response" : "Error response";
        }

        return "Response variant";
    }

    private static string DescribeType(JsonObject? schema)
    {
        if (schema is null)
        {
            return "unspecified";
        }

        string? declaredType = null;
        if (schema["type"] is JsonValue typeValue
            && typeValue.TryGetValue<string>(out var type))
        {
            declaredType = type;
        }
        else if (schema["type"] is JsonArray types)
        {
            declaredType = string.Join(" | ", types.Select(static item => item?.GetValue<string>()));
        }

        if (schema["enum"] is JsonArray options)
        {
            var values = string.Join(", ", options.Select(static option => option?.ToJsonString() ?? "null"));
            var optionTypes = options
                .Select(static option => option is null ? "null" : DescribeJsonValueType(option))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var enumType = optionTypes.Length == 1 ? optionTypes[0] : "mixed";
            return $"{declaredType ?? enumType} enum ({values})";
        }

        if (schema.ContainsKey("const"))
        {
            var constant = schema["const"];
            return $"{declaredType ?? DescribeJsonValueType(constant)} constant ({constant?.ToJsonString() ?? "null"})";
        }

        return declaredType ?? (schema.ContainsKey("oneOf") ? "oneOf" : "object");
    }

    private static string DescribeJsonValueType(JsonNode? value)
    {
        return value?.GetValueKind() switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Array => "array",
            JsonValueKind.Object => "object",
            _ => "null",
        };
    }

    private static JsonObject GetRequiredObject(JsonObject parent, string propertyName, string toolName)
    {
        return parent[propertyName] as JsonObject
            ?? throw new InvalidOperationException($"Tool '{toolName}' does not publish object property '{propertyName}'.");
    }

    private static JsonObject CreateCatalogSchema()
    {
        // The embedded schema is a compile-time JSON object literal; parsing cannot produce another node kind.
        return JsonNode.Parse(
            $$"""
            {
              "$id": "{{_catalogSchemaId}}",
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": ["formatVersion", "productVersion", "sourceTag", "commit", "tools"],
              "properties": {
                "$schema": { "type": "string" },
                "formatVersion": { "const": "roslyn-workbench-tool-reference/v1" },
                "productVersion": { "type": "string", "minLength": 1 },
                "sourceTag": { "type": "string", "minLength": 1 },
                "commit": { "type": "string", "minLength": 1 },
                "tools": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "required": ["name", "title", "area", "category", "operationKind", "summary", "availability", "detailUrl", "documentationUrl"],
                    "properties": {
                      "name": { "type": "string", "minLength": 1 },
                      "title": { "type": "string", "minLength": 1 },
                      "area": { "enum": ["Server", "CorePlugin", "CodeAction"] },
                      "category": { "type": "string", "minLength": 1 },
                      "operationKind": { "enum": ["Query", "Mutation"] },
                      "summary": { "type": "string", "minLength": 1 },
                      "availability": { "type": "string", "minLength": 1 },
                      "detailUrl": { "type": "string", "minLength": 1, "description": "Relative URL of the complete machine-readable tool definition." },
                      "documentationUrl": { "type": "string", "format": "uri", "description": "Absolute URL of the human-readable tool documentation." }
                    },
                    "additionalProperties": false
                  }
                }
              },
              "additionalProperties": false
            }
            """)!.AsObject();
    }

    private static JsonObject CreateDetailSchema()
    {
        // The embedded schema is a compile-time JSON object literal; parsing cannot produce another node kind.
        return JsonNode.Parse(
            $$"""
            {
              "$id": "{{_detailSchemaId}}",
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": ["formatVersion", "productVersion", "sourceTag", "commit", "name", "title", "area", "category", "operationKind", "summary", "availability", "documentationUrl", "tool", "examples"],
              "properties": {
                "$schema": { "type": "string" },
                "formatVersion": { "const": "roslyn-workbench-tool-reference/v1" },
                "productVersion": { "type": "string", "minLength": 1 },
                "sourceTag": { "type": "string", "minLength": 1 },
                "commit": { "type": "string", "minLength": 1 },
                "name": { "type": "string", "minLength": 1 },
                "title": { "type": "string", "minLength": 1 },
                "area": { "enum": ["Server", "CorePlugin", "CodeAction"] },
                "category": { "type": "string", "minLength": 1 },
                "operationKind": { "enum": ["Query", "Mutation"] },
                "summary": { "type": "string", "minLength": 1 },
                "availability": { "type": "string", "minLength": 1 },
                "documentationUrl": { "type": "string", "format": "uri", "description": "Absolute URL of the human-readable tool documentation." },
                "tool": {
                  "type": "object",
                  "required": ["name", "description", "inputSchema", "outputSchema"],
                  "properties": {
                    "name": { "type": "string", "minLength": 1 },
                    "description": { "type": "string", "minLength": 1 },
                    "inputSchema": { "type": "object" },
                    "outputSchema": { "type": "object" }
                  }
                },
                "examples": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "required": ["workflowId", "workflowTitle", "step", "id", "title", "purpose", "expectedOutcome", "request"],
                    "properties": {
                      "workflowId": { "type": "string", "minLength": 1 },
                      "workflowTitle": { "type": "string", "minLength": 1 },
                      "step": { "type": "integer", "minimum": 1 },
                      "id": { "type": "string", "minLength": 1 },
                      "title": { "type": "string", "minLength": 1 },
                      "purpose": { "type": "string", "minLength": 1 },
                      "expectedOutcome": { "type": "string", "minLength": 1 },
                      "representativeResponse": { "type": ["object", "null"] },
                      "request": { "type": "object" }
                    },
                    "additionalProperties": false
                  }
                }
              },
              "additionalProperties": false
            }
            """)!.AsObject();
    }

    private static void WriteJson(string path, JsonNode value)
    {
        WriteText(path, value.ToJsonString(_jsonOptions));
    }

    private static void WriteText(string path, string content)
    {
        File.WriteAllText(path, content.TrimEnd() + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeTable(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    private static string CreateDocumentationUrl(string sourceTag, string toolName)
    {
        var documentationVersion = sourceTag == _developmentSourceTag ? "dev" : sourceTag;
        return $"{_documentationBaseUrl}{Uri.EscapeDataString(documentationVersion)}/reference/tools/{Uri.EscapeDataString(toolName)}.html";
    }

    private static string EscapePointer(string value)
    {
        return value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }
}
