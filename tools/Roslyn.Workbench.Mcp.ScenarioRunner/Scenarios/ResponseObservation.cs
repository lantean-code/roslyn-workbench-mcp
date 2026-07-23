using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

internal sealed record ResponseObservation
{
    public required int Bytes { get; init; }

    public required string Sha256 { get; init; }

    public IReadOnlyList<BoundedCollectionObservation> BoundedCollections { get; init; } = [];

    public bool? MutationStaged { get; init; }

    public static ResponseObservation Create(CallToolResult result)
    {
        var boundedCollections = new List<BoundedCollectionObservation>();
        string content;
        bool? mutationStaged = null;
        if (result.StructuredContent is JsonElement structuredContent)
        {
            content = structuredContent.GetRawText();
            CollectBoundedCollections(structuredContent, "$", boundedCollections);
            mutationStaged = GetMutationStaged(structuredContent);
        }
        else
        {
            content = JsonSerializer.Serialize(result.Content);
        }

        return new ResponseObservation
        {
            Bytes = Encoding.UTF8.GetByteCount(content),
            Sha256 = Hash(content),
            BoundedCollections = boundedCollections,
            MutationStaged = mutationStaged,
        };
    }

    private static void CollectBoundedCollections(
        JsonElement element,
        string path,
        List<BoundedCollectionObservation> observations)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array
            && element.TryGetProperty("hasMore", out var hasMore)
            && hasMore.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            var itemHashes = new List<string>();
            foreach (var item in items.EnumerateArray())
            {
                itemHashes.Add(Hash(item.GetRawText()));
            }

            observations.Add(new BoundedCollectionObservation
            {
                Path = path,
                ItemCount = items.GetArrayLength(),
                HasMore = hasMore.GetBoolean(),
                OrderedItemSha256 = itemHashes,
            });
        }

        foreach (var property in element.EnumerateObject())
        {
            CollectBoundedCollections(property.Value, $"{path}.{property.Name}", observations);
        }
    }

    private static bool? GetMutationStaged(JsonElement structuredContent)
    {
        if (!structuredContent.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("staged", out var staged)
            || staged.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        return staged.GetBoolean();
    }

    private static string Hash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
