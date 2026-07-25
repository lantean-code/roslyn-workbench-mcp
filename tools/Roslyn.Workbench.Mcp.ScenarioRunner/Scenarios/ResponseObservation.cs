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

    public CodeActionTokenObservation? CodeActionTokens { get; init; }

    public bool? MutationStaged { get; init; }

    public static ResponseObservation Create(CallToolResult result)
    {
        var boundedCollections = new List<BoundedCollectionObservation>();
        var codeActionTokenLengths = new List<int>();
        string content;
        bool? mutationStaged = null;
        if (result.StructuredContent is JsonElement structuredContent)
        {
            content = structuredContent.GetRawText();
            CollectResponseDetails(
                structuredContent,
                "$",
                boundedCollections,
                codeActionTokenLengths);

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
            CodeActionTokens = CreateCodeActionTokenObservation(codeActionTokenLengths),
            MutationStaged = mutationStaged,
        };
    }

    private static void CollectResponseDetails(
        JsonElement element,
        string path,
        List<BoundedCollectionObservation> boundedCollections,
        List<int> codeActionTokenLengths)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                CollectResponseDetails(
                    item,
                    $"{path}[{index}]",
                    boundedCollections,
                    codeActionTokenLengths);

                index++;
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("actionId", out var actionId)
            && actionId.ValueKind == JsonValueKind.String)
        {
            var token = actionId.GetString();
            if (token is not null)
            {
                codeActionTokenLengths.Add(Encoding.UTF8.GetByteCount(token));
            }
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

            int? totalCount = null;
            if (element.TryGetProperty("totalCount", out var totalCountElement)
                && totalCountElement.TryGetInt32(out var parsedTotalCount))
            {
                totalCount = parsedTotalCount;
            }

            boundedCollections.Add(new BoundedCollectionObservation
            {
                Path = path,
                ItemCount = items.GetArrayLength(),
                HasMore = hasMore.GetBoolean(),
                TotalCount = totalCount,
                OrderedItemSha256 = itemHashes,
            });
        }

        foreach (var property in element.EnumerateObject())
        {
            CollectResponseDetails(
                property.Value,
                $"{path}.{property.Name}",
                boundedCollections,
                codeActionTokenLengths);
        }
    }

    private static CodeActionTokenObservation? CreateCodeActionTokenObservation(
        List<int> tokenLengths)
    {
        if (tokenLengths.Count == 0)
        {
            return null;
        }

        var maximumBytes = 0;
        long totalBytes = 0;
        foreach (var tokenLength in tokenLengths)
        {
            maximumBytes = Math.Max(maximumBytes, tokenLength);
            totalBytes += tokenLength;
        }

        return new CodeActionTokenObservation
        {
            Count = tokenLengths.Count,
            MaximumBytes = maximumBytes,
            TotalBytes = totalBytes,
        };
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
