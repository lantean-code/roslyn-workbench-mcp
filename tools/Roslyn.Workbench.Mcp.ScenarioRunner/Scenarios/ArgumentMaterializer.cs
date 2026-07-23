using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

internal static class ArgumentMaterializer
{
    private const string _repositoryRootToken = "${repositoryRoot}";
    private const string _workspaceIdToken = "${workspaceId}";

    public static IReadOnlyDictionary<string, object?> Materialize(
        JsonElement arguments,
        string workspaceId,
        string repositoryRoot)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Tool arguments must be a JSON object.");
        }

        var materialized = ConvertElement(arguments, workspaceId, repositoryRoot);
        return materialized as IReadOnlyDictionary<string, object?>
            ?? throw new InvalidOperationException("Materialising a JSON object did not produce a dictionary.");
    }

    private static object? ConvertElement(JsonElement element, string workspaceId, string repositoryRoot)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var values = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                {
                    values[property.Name] = ConvertElement(property.Value, workspaceId, repositoryRoot);
                }

                return values;

            case JsonValueKind.Array:
                var items = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    items.Add(ConvertElement(item, workspaceId, repositoryRoot));
                }

                return items;

            case JsonValueKind.String:
                return ReplaceTokens(element.GetString() ?? string.Empty, workspaceId, repositoryRoot);

            case JsonValueKind.Number:
                return element.TryGetInt64(out var integer) ? integer : element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null;

            default:
                throw new InvalidDataException($"Unsupported JSON value kind '{element.ValueKind}'.");
        }
    }

    private static string ReplaceTokens(string value, string workspaceId, string repositoryRoot)
    {
        return value
            .Replace(_workspaceIdToken, workspaceId, StringComparison.Ordinal)
            .Replace(_repositoryRootToken, repositoryRoot, StringComparison.Ordinal);
    }
}
