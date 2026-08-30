using System.Text;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class InputSchemaBudget
{
    public const int MaximumSizeInBytes = 5_000;

    public static int GetSizeInBytes(JsonElement schema)
    {
        return Encoding.UTF8.GetByteCount(schema.GetRawText());
    }
}
