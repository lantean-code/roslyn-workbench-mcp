using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

internal static class ToolContinuationReader
{
    public static ToolContinuationObservation? Read(JsonElement content)
    {
        if (!content.TryGetProperty("continuation", out var continuation)
            || continuation.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var kind = ReadRequiredString(continuation, "kind");
        var instruction = ReadRequiredString(continuation, "instruction");
        var tool = ReadOptionalString(continuation, "tool");
        var tools = ReadOptionalStringArray(continuation, "tools");

        return new ToolContinuationObservation
        {
            Kind = kind,
            Tool = tool,
            Tools = tools,
            Instruction = instruction,
        };
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        var value = ReadOptionalString(element, propertyName);
        if (value is null)
        {
            throw new InvalidDataException(
                $"The tool continuation contains no '{propertyName}' string.");
        }

        return value;
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static List<string>? ReadOptionalStringArray(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"The tool continuation property '{propertyName}' is not an array.");
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } value)
            {
                throw new InvalidDataException(
                    $"The tool continuation property '{propertyName}' contains a non-string value.");
            }

            values.Add(value);
        }

        return values;
    }
}
