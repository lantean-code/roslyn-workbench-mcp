using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Checks the code-derived security surface, validates authored mappings and produces reference documentation.
/// </summary>
internal static class SecurityReferenceGenerator
{
    private const string _surfaceFormat = "roslyn-workbench-security-surface/v1";

    /// <summary>
    /// Gets the canonical serializer settings shared by security-reference artifacts.
    /// </summary>
    internal static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// Generates and validates the security reference.
    /// </summary>
    /// <param name="options">The generation inputs and output.</param>
    /// <param name="cancellationToken">The token used to cancel generation.</param>
    /// <returns>A task that completes when output has been written.</returns>
    public static async Task GenerateAsync(
        SecurityReferenceGeneratorOptions options,
        CancellationToken cancellationToken)
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), $"roslyn-workbench-security-reference-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stateDirectory);
        try
        {
            PublishedErrorCodeSourceValidator.Validate(options.RepositoryRoot);

            ValidateDocumentSchema(
                options.ManifestFile,
                Path.Combine(Path.GetDirectoryName(options.ManifestFile)!, "schemas", "security-invariants.schema.json"));

            var manifest = LoadManifest(options.ManifestFile);
            var evidenceValidator = new SecurityEvidenceValidator(options.RepositoryRoot);
            var evidence = manifest.Entries.SelectMany(static entry => entry.Evidence);
            await evidenceValidator.ValidateAsync(evidence, cancellationToken);

            var surface = await SecuritySurfaceCollector.CollectAsync(stateDirectory, cancellationToken);
            var currentBaseline = CreateSurfaceDocument(surface);
            if (options.UpdateBaseline)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(options.BaselineFile)!);
                await File.WriteAllTextAsync(
                    options.BaselineFile,
                    currentBaseline,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    cancellationToken);
            }
            else
            {
                ValidateBaseline(options.BaselineFile, currentBaseline);
            }

            ValidateDocumentSchema(
                options.BaselineFile,
                Path.Combine(Path.GetDirectoryName(options.BaselineFile)!, "schemas", "security-surface.schema.json"));
            SecurityManifestValidator.Validate(manifest, surface);

            SecurityReferenceWriter.Write(options.OutputDirectory, manifest, surface, options.SourceRevision);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    private static string CreateSurfaceDocument(IReadOnlyList<SecuritySurfaceItem> surface)
    {
        var root = new JsonObject
        {
            ["$schema"] = "schemas/security-surface.schema.json",
            ["format"] = _surfaceFormat,
            ["items"] = new JsonArray(surface.Select(CreateSurfaceNode).ToArray()),
        };

        return root.ToJsonString(SerializerOptions) + Environment.NewLine;
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

    private static void ValidateBaseline(string baselineFile, string currentBaseline)
    {
        if (!File.Exists(baselineFile))
        {
            throw new FileNotFoundException("The checked-in security surface baseline was not found. Run with --update-security-baseline to create it.", baselineFile);
        }

        var expected = JsonNode.Parse(File.ReadAllText(baselineFile));
        var actual = JsonNode.Parse(currentBaseline);
        if (!JsonNode.DeepEquals(expected, actual))
        {
            throw new InvalidOperationException("The compiled security surface differs from docs/security/security-surface-v1.json. Review the change and run with --update-security-baseline explicitly.");
        }
    }

    private static SecurityInvariantManifest LoadManifest(string manifestFile)
    {
        if (!File.Exists(manifestFile))
        {
            throw new FileNotFoundException("The authored security invariant manifest was not found.", manifestFile);
        }

        return JsonSerializer.Deserialize<SecurityInvariantManifest>(
            File.ReadAllText(manifestFile),
            SerializerOptions)
            ?? throw new InvalidOperationException("The security invariant manifest is empty.");
    }

    private static void ValidateDocumentSchema(string documentFile, string schemaFile)
    {
        if (!File.Exists(schemaFile))
        {
            throw new FileNotFoundException("A required security-reference JSON schema was not found.", schemaFile);
        }

        using var schemaDocument = JsonDocument.Parse(File.ReadAllText(schemaFile));
        using var valueDocument = JsonDocument.Parse(File.ReadAllText(documentFile));
        var buildOptions = new BuildOptions
        {
            SchemaRegistry = new SchemaRegistry(),
        };

        var schema = JsonSchema.Build(schemaDocument.RootElement, buildOptions);
        if (!schema.Evaluate(valueDocument.RootElement).IsValid)
        {
            throw new InvalidOperationException($"Security-reference document '{documentFile}' does not satisfy '{schemaFile}'.");
        }
    }

}
