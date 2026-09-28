using System.Text;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Writes deterministic human-readable and machine-readable security reference output.
/// </summary>
internal static class SecurityReferenceWriter
{
    private const string _publicationFormat = "roslyn-workbench-security-reference/v1";

    /// <summary>
    /// Writes the resolved security reference.
    /// </summary>
    /// <param name="outputDirectory">The generated reference directory.</param>
    /// <param name="manifest">The validated authored manifest.</param>
    /// <param name="surface">The validated code-derived surface.</param>
    /// <param name="sourceRevision">The immutable Git revision represented by the generated reference.</param>
    public static void Write(
        string outputDirectory,
        SecurityInvariantManifest manifest,
        IReadOnlyList<SecuritySurfaceItem> surface,
        string sourceRevision)
    {
        PrepareOutputDirectory(outputDirectory);
        var surfaceByKey = surface.ToDictionary(static item => item.Key, StringComparer.Ordinal);
        var publication = CreatePublication(manifest, surfaceByKey);
        File.WriteAllText(
            Path.Combine(outputDirectory, "security-reference.json"),
            publication.ToJsonString(SecurityReferenceGenerator.SerializerOptions) + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        File.WriteAllText(
            Path.Combine(outputDirectory, "index.md"),
            CreateMarkdown(manifest, surfaceByKey, sourceRevision),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static JsonObject CreatePublication(
        SecurityInvariantManifest manifest,
        IReadOnlyDictionary<string, SecuritySurfaceItem> surfaceByKey)
    {
        return new JsonObject
        {
            ["format"] = _publicationFormat,
            ["trustBoundary"] = "The connected MCP client is authorised and belongs to the local trust boundary. These controls protect against mistakes, misfires, stale assumptions and unexpectedly broad operations; they do not defend against hostile code already exercising the same operating-system authority.",
            ["entries"] = new JsonArray(manifest.Entries
                .OrderBy(static entry => entry.Classification, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Id, StringComparer.Ordinal)
                .Select(entry => CreateEntryNode(entry, surfaceByKey))
                .ToArray()),
        };
    }

    private static JsonObject CreateEntryNode(
        SecurityInvariantEntry entry,
        IReadOnlyDictionary<string, SecuritySurfaceItem> surfaceByKey)
    {
        return new JsonObject
        {
            ["id"] = entry.Id,
            ["title"] = entry.Title,
            ["classification"] = entry.Classification,
            ["actor"] = entry.Actor,
            ["outcome"] = entry.Outcome,
            ["boundary"] = entry.Boundary,
            ["residualRisk"] = entry.ResidualRisk,
            ["surface"] = new JsonArray(entry.SurfaceKeys
                .Order(StringComparer.Ordinal)
                .Select(key => CreateSurfaceNode(surfaceByKey[key]))
                .ToArray()),
            ["evidence"] = new JsonArray(entry.Evidence
                .OrderBy(static evidence => evidence.Level, StringComparer.Ordinal)
                .ThenBy(static evidence => evidence.Test, StringComparer.Ordinal)
                .Select(CreateEvidenceNode)
                .ToArray()),
        };
    }

    private static JsonObject CreateSurfaceNode(SecuritySurfaceItem item)
    {
        return new JsonObject
        {
            ["key"] = item.Key,
            ["category"] = item.Category,
            ["facts"] = item.Facts.DeepClone(),
        };
    }

    private static JsonObject CreateEvidenceNode(SecurityEvidenceReference evidence)
    {
        return new JsonObject
        {
            ["level"] = evidence.Level,
            ["project"] = evidence.Project,
            ["source"] = evidence.Source,
            ["test"] = evidence.Test,
        };
    }

    private static string CreateMarkdown(
        SecurityInvariantManifest manifest,
        IReadOnlyDictionary<string, SecuritySurfaceItem> surfaceByKey,
        string sourceRevision)
    {
        var content = new StringBuilder();
        content.AppendLine("# Security guarantees and evidence");
        content.AppendLine();
        content.AppendLine("Roslyn Workbench treats the connected local MCP client as authorised. These controls reduce mistakes, misfires, stale assumptions and unexpectedly broad operations; they are not an operating-system sandbox and do not defend against hostile code already running with the same authority.");
        content.AppendLine();

        AppendSection(content, "Enforced invariants", "enforced", manifest, surfaceByKey, sourceRevision);
        AppendSection(content, "Delegated responsibilities", "delegated", manifest, surfaceByKey, sourceRevision);
        AppendSection(content, "Limitations", "limitation", manifest, surfaceByKey, sourceRevision);
        AppendSurfaceAppendix(content, surfaceByKey.Values);
        return content.ToString();
    }

    private static void AppendSection(
        StringBuilder content,
        string title,
        string classification,
        SecurityInvariantManifest manifest,
        IReadOnlyDictionary<string, SecuritySurfaceItem> surfaceByKey,
        string sourceRevision)
    {
        content.Append("## ").AppendLine(title);
        content.AppendLine();
        foreach (var entry in manifest.Entries
            .Where(entry => entry.Classification == classification)
            .OrderBy(static entry => entry.Id, StringComparer.Ordinal))
        {
            content.Append("### ").AppendLine(entry.Title);
            content.AppendLine();
            content.Append("**Actor:** ").AppendLine(entry.Actor);
            content.AppendLine();
            content.AppendLine(entry.Outcome);
            content.AppendLine();
            content.Append("**Boundary:** ").AppendLine(entry.Boundary);
            content.AppendLine();
            content.Append("**Residual risk:** ").AppendLine(entry.ResidualRisk);
            content.AppendLine();

            if (entry.SurfaceKeys.Count > 0)
            {
                content.AppendLine("**Covered surface:**");
                content.AppendLine();
                foreach (var key in entry.SurfaceKeys.Order(StringComparer.Ordinal))
                {
                    content.Append("- `").Append(surfaceByKey[key].Key).AppendLine("`");
                }

                content.AppendLine();
            }

            if (entry.Evidence.Count > 0)
            {
                content.AppendLine("**Automated evidence:**");
                content.AppendLine();
                foreach (var evidence in entry.Evidence
                    .OrderBy(static evidence => evidence.Level, StringComparer.Ordinal)
                    .ThenBy(static evidence => evidence.Test, StringComparer.Ordinal))
                {
                    content.Append("- ").Append(evidence.Level).Append(": [`")
                        .Append(evidence.Test).Append("`](https://github.com/lantean-code/roslyn-workbench-mcp/blob/")
                        .Append(sourceRevision).Append('/')
                        .Append(evidence.Source.Replace('\\', '/')).AppendLine(")");
                }

                content.AppendLine();
            }
        }
    }

    private static void AppendSurfaceAppendix(StringBuilder content, IEnumerable<SecuritySurfaceItem> surface)
    {
        content.AppendLine("## Public-surface appendix");
        content.AppendLine();
        content.AppendLine("The following keys and facts are derived from compiled contracts and production Host composition. Complete tool schemas remain in the tool reference.");
        content.AppendLine();
        content.AppendLine("| Surface key | Derived facts |");
        content.AppendLine("| --- | --- |");
        foreach (var item in surface.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            var facts = item.Facts.ToJsonString().Replace("|", "\\|", StringComparison.Ordinal);
            content.Append("| `").Append(item.Key).Append("` | `").Append(facts).AppendLine("` |");
        }
    }

    private static void PrepareOutputDirectory(string outputDirectory)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
        if (!normalized.EndsWith(
            Path.Combine("content", "reference", "security"),
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Security-reference output must be a content/reference/security directory.");
        }

        Directory.CreateDirectory(normalized);
    }
}
