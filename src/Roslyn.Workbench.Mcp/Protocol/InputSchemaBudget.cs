using System.Text;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Measures input schemas against the size budget used for MCP publication.
/// </summary>
internal static class InputSchemaBudget
{
    /// <summary>
    /// Defines the maximum UTF-8 size of one published tool input schema.
    /// </summary>
    public const int MaximumSizeInBytes = 5_000;

    /// <summary>
    /// Measures the serialized UTF-8 size of a schema.
    /// </summary>
    /// <param name="schema">The JSON schema being inspected or transformed.</param>
    /// <returns>The number of bytes required to publish the schema as JSON.</returns>
    public static int GetSizeInBytes(JsonElement schema)
    {
        return Encoding.UTF8.GetByteCount(schema.GetRawText());
    }
}
