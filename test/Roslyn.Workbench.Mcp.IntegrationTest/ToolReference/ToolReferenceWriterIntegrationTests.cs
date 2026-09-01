using System.Text.Json.Nodes;
using Roslyn.Workbench.Mcp.IntegrationTestSupport;
using Roslyn.Workbench.Mcp.ToolReferenceGenerator;

namespace Roslyn.Workbench.Mcp.Test.ToolReference;

[Collection(ToolReferenceGenerationCollectionDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ToolReferenceWriterIntegrationTests
{
    [Fact]
    public async Task GIVEN_RepresentativeSchemaShapes_WHEN_WritingReference_THEN_ShouldExplainRelevantContractDetails()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-writer-tests");
        var outputDirectory = Path.Combine(directory.DirectoryPath, "reference", "tools");
        var identity = CreateIdentity();
        var inputSchema = CreateRepresentativeInputSchema();
        var outputSchema = CreateRepresentativeOutputSchema();
        var protocolTool = CreateProtocolTool(inputSchema, outputSchema);
        var entry = CreateEntry(protocolTool);

        ToolReferenceWriter.Write(outputDirectory, identity, "roslyn-workbench-tool-reference/v1", [entry]);

        var page = await File.ReadAllTextAsync(
            Path.Combine(outputDirectory, "sample-tool.md"),
            TestContext.Current.CancellationToken);
        page.Should().Contain("The tool does not publish behaviour annotations.");
        page.Should().Contain("<code>boolean constant (true)</code>");
        page.Should().Contain("<code>array constant ([])</code>");
        page.Should().Contain("<code>object constant ({})</code>");
        page.Should().Contain("<code>null constant (null)</code>");
        page.Should().Contain("<code>string enum (&quot;Fast&quot;, &quot;Safe&quot;)</code>");
        page.Should().Contain("<code>string &#124; null</code>");
        page.Should().Contain("<code>oneOf</code>");
        page.Should().Contain("<code>unspecified</code>");
        page.Should().Contain("uses `oneOf` validation");
        page.Should().Contain("uses `anyOf` validation");
        page.Should().Contain("uses `allOf` validation");
        page.Should().Contain("uses `if` validation");
        page.Should().Contain("uses `then` validation");
        page.Should().Contain("uses `else` validation");
        page.Should().Contain("uses `not` validation");
        page.Should().Contain("### Success response");
        page.Should().Contain("### Error response");
        page.Should().Contain("### Response variant");
        page.Should().Contain("### Bounded collections and continuation");
        page.Should().Contain("### Outcomes");
        page.Should().Contain("`Completed`");
        page.Should().Contain("`Deferred`");

        var catalog = await File.ReadAllTextAsync(
            Path.Combine(outputDirectory, "catalog.json"),
            TestContext.Current.CancellationToken);
        catalog.Should().Contain("\"documentationUrl\": \"https://lantean-code.github.io/roslyn-workbench-mcp/1.0.0/reference/tools/sample-tool.html\"");

        var detail = await File.ReadAllTextAsync(
            Path.Combine(outputDirectory, "data", "sample-tool.json"),
            TestContext.Current.CancellationToken);
        detail.Should().Contain("\"documentationUrl\": \"https://lantean-code.github.io/roslyn-workbench-mcp/1.0.0/reference/tools/sample-tool.html\"");
    }

    [Fact]
    public async Task GIVEN_ResponseWithoutContinuation_WHEN_WritingReference_THEN_ShouldOmitContinuationGuidance()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-writer-tests");
        var outputDirectory = Path.Combine(directory.DirectoryPath, "reference", "tools");
        var identity = CreateIdentity();
        var inputSchema = new JsonObject();
        var outputSchema = new JsonObject();
        var protocolTool = CreateProtocolTool(inputSchema, outputSchema);
        var entry = CreateEntry(protocolTool);

        ToolReferenceWriter.Write(outputDirectory, identity, "roslyn-workbench-tool-reference/v1", [entry]);

        var page = await File.ReadAllTextAsync(
            Path.Combine(outputDirectory, "sample-tool.md"),
            TestContext.Current.CancellationToken);
        page.Should().NotContain("### Continuations and required actions");
    }

    [Theory]
    [InlineData("inputSchema")]
    [InlineData("outputSchema")]
    public void GIVEN_ProtocolToolMissingSchema_WHEN_WritingReference_THEN_ShouldRejectTool(string missingProperty)
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-writer-tests");
        var outputDirectory = Path.Combine(directory.DirectoryPath, "reference", "tools");
        var identity = CreateIdentity();
        var inputSchema = new JsonObject();
        var outputSchema = new JsonObject();
        var protocolTool = CreateProtocolTool(inputSchema, outputSchema);
        protocolTool.Remove(missingProperty);
        var entry = CreateEntry(protocolTool);

        var action = () => ToolReferenceWriter.Write(
            outputDirectory,
            identity,
            "roslyn-workbench-tool-reference/v1",
            [entry]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{missingProperty}*");
    }

    private static ToolReferenceBuildIdentity CreateIdentity()
    {
        var identity = new ToolReferenceBuildIdentity
        {
            ProductVersion = "1.0.0",
            SourceTag = "1.0.0",
            Commit = "Commit",
        };

        return identity;
    }

    private static ToolReferenceEntry CreateEntry(JsonObject protocolTool)
    {
        var entry = new ToolReferenceEntry
        {
            Name = "sample-tool",
            Title = "Sample Tool",
            Area = "Server",
            Category = "Server lifecycle",
            OperationKind = "Query",
            Summary = "Explains representative schema shapes.",
            Availability = "Built in and published by default.",
            ProtocolTool = protocolTool,
            Examples = [],
        };

        return entry;
    }

    private static JsonObject CreateProtocolTool(JsonObject inputSchema, JsonObject outputSchema)
    {
        var protocolTool = new JsonObject
        {
            ["name"] = "sample-tool",
            ["description"] = "Explains representative schema shapes.",
            ["inputSchema"] = inputSchema,
            ["outputSchema"] = outputSchema,
        };

        return protocolTool;
    }

    private static JsonObject CreateRepresentativeInputSchema()
    {
        var constantSchema = new JsonObject
        {
            ["const"] = true,
        };

        var constantArray = new JsonArray();
        var constantArraySchema = new JsonObject
        {
            ["const"] = constantArray,
        };

        var constantObject = new JsonObject();
        var constantObjectSchema = new JsonObject
        {
            ["const"] = constantObject,
        };

        var constantNullSchema = new JsonObject
        {
            ["const"] = null,
        };

        var modeValues = new JsonArray("Fast", "Safe");
        var modeSchema = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = modeValues,
        };

        var optionalTypes = new JsonArray("string", "null");
        var optionalSchema = new JsonObject
        {
            ["type"] = optionalTypes,
        };

        var choiceVariant = new JsonObject();
        var choiceVariants = new JsonArray(choiceVariant);
        var choiceSchema = new JsonObject
        {
            ["oneOf"] = choiceVariants,
        };

        var properties = new JsonObject
        {
            ["constant"] = constantSchema,
            ["constantArray"] = constantArraySchema,
            ["constantObject"] = constantObjectSchema,
            ["constantNull"] = constantNullSchema,
            ["mode"] = modeSchema,
            ["optional"] = optionalSchema,
            ["choice"] = choiceSchema,
            ["unknown"] = "not-a-schema-object",
        };

        var required = new JsonArray("mode", null);
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["oneOf"] = new JsonArray(),
            ["anyOf"] = new JsonArray(),
            ["allOf"] = new JsonArray(),
            ["if"] = new JsonObject(),
            ["then"] = new JsonObject(),
            ["else"] = new JsonObject(),
            ["not"] = new JsonObject(),
        };

        return schema;
    }

    private static JsonObject CreateRepresentativeOutputSchema()
    {
        var succeededSchema = new JsonObject
        {
            ["const"] = true,
        };

        var outcomeValues = new JsonArray("Completed", 1);
        var outcomeSchema = new JsonObject
        {
            ["enum"] = outcomeValues,
        };

        var resultProperties = new JsonObject
        {
            ["items"] = new JsonObject(),
            ["hasMore"] = new JsonObject(),
        };

        var resultsSchema = new JsonObject
        {
            ["properties"] = resultProperties,
        };

        var successProperties = new JsonObject
        {
            ["ok"] = succeededSchema,
            ["outcome"] = outcomeSchema,
            ["results"] = resultsSchema,
        };

        var failedSchema = new JsonObject
        {
            ["const"] = false,
        };

        var errorProperties = new JsonObject
        {
            ["ok"] = failedSchema,
            ["continuation"] = new JsonObject(),
        };

        var numberSchema = new JsonObject
        {
            ["type"] = "number",
        };

        var nestedOutcomeSchema = new JsonObject
        {
            ["const"] = "Deferred",
        };

        var nestedProperties = new JsonObject
        {
            ["outcome"] = nestedOutcomeSchema,
        };

        var nestedSchema = new JsonObject
        {
            ["properties"] = nestedProperties,
        };

        var genericProperties = new JsonObject
        {
            ["value"] = numberSchema,
            ["nested"] = nestedSchema,
        };

        var successVariant = new JsonObject
        {
            ["properties"] = successProperties,
        };

        var errorVariant = new JsonObject
        {
            ["properties"] = errorProperties,
        };

        var genericVariant = new JsonObject
        {
            ["properties"] = genericProperties,
        };

        var variants = new JsonArray
        {
            "not-an-object",
            successVariant,
            errorVariant,
            genericVariant,
        };

        var schema = new JsonObject
        {
            ["oneOf"] = variants,
        };

        return schema;
    }
}
