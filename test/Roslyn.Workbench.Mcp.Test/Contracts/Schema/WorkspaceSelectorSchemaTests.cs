using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test.Contracts.Schema;

[Trait("Category", "Contract")]
public sealed class WorkspaceSelectorSchemaTests
{
    private readonly ToolSchemaFactory _target = new(new McpSdkSchemaProvider());

    [Theory]
    [InlineData("workspace", "anyOf", 3)]
    [InlineData("project", "anyOf", 4)]
    [InlineData("document", "oneOf", 2)]
    [InlineData("location", "oneOf", 2)]
    [InlineData("symbol", "oneOf", 2)]
    [InlineData("scope", "oneOf", 4)]
    public void GIVEN_WorkspaceSelectorProperty_WHEN_PublishingSchema_THEN_ShouldPublishSemanticAlternatives(
        string propertyName,
        string alternativeKeyword,
        int expectedAlternativeCount)
    {
        var schema = _target.CreateInputSchema<SelectorSchemaRequest>();

        var selectorSchema = ResolvePropertyObjectSchema(schema, propertyName);
        var constraint = selectorSchema.GetProperty("allOf")[0];

        constraint.GetProperty(alternativeKeyword).GetArrayLength().Should().Be(expectedAlternativeCount);
    }

    [Fact]
    public void GIVEN_ScopeSelector_WHEN_PublishingSchema_THEN_ShouldPublishKindSpecificRequirements()
    {
        var schema = _target.CreateInputSchema<SelectorSchemaRequest>();

        var scopeSchema = ResolvePropertyObjectSchema(schema, "scope");
        var branches = scopeSchema.GetProperty("allOf")[0].GetProperty("oneOf");

        branches[0].GetProperty("properties").GetProperty("kind").GetProperty("const").GetString().Should().Be("Solution");
        branches[0].TryGetProperty("required", out _).Should().BeFalse();
        branches[1].GetProperty("required").EnumerateArray().Select(static item => item.GetString()).Should().Contain("project");
        branches[2].GetProperty("required").EnumerateArray().Select(static item => item.GetString()).Should().Contain("document");
        branches[3].GetProperty("properties").GetProperty("projects").GetProperty("minItems").GetInt32().Should().Be(1);
    }

    [Fact]
    public void GIVEN_ProjectSelectorsInDictionary_WHEN_PublishingSchema_THEN_ShouldPublishValueConstraint()
    {
        var schema = _target.CreateInputSchema<ProjectDictionarySchemaRequest>();
        var projectsSchema = ResolvePropertyObjectSchema(schema, "projects");
        var valueSchema = ResolveObjectSchema(schema, projectsSchema.GetProperty("additionalProperties"));
        var constraint = valueSchema.GetProperty("allOf")[0];

        constraint.GetProperty("anyOf").GetArrayLength().Should().Be(4);
    }

    [Theory]
    [InlineData("document", 0, "documentId")]
    [InlineData("document", 1, "path")]
    [InlineData("symbol", 0, "documentationCommentId")]
    public void GIVEN_ExclusiveStringSelector_WHEN_PublishingSchema_THEN_ShouldAllowUnusedBlankAlternative(
        string selectorName,
        int branchIndex,
        string unusedPropertyName)
    {
        var schema = _target.CreateInputSchema<SelectorSchemaRequest>();
        var selectorSchema = ResolvePropertyObjectSchema(schema, selectorName);
        var branch = selectorSchema.GetProperty("allOf")[0].GetProperty("oneOf")[branchIndex];
        var unusedProperty = branch.GetProperty("properties").GetProperty(unusedPropertyName);

        unusedProperty.GetProperty("not").GetProperty("pattern").GetString().Should().Be(@"\S");
    }

    [Fact]
    public void GIVEN_ProjectSelectorsInCollection_WHEN_PublishingSchema_THEN_ShouldPublishElementConstraint()
    {
        var schema = _target.CreateInputSchema<ProjectCollectionSchemaRequest>();
        var projectsSchema = ResolvePropertyObjectSchema(schema, "projects");
        var itemSchema = ResolveObjectSchema(schema, projectsSchema.GetProperty("items"));
        var constraint = itemSchema.GetProperty("allOf")[0];

        constraint.GetProperty("anyOf").GetArrayLength().Should().Be(4);
    }

    private static JsonElement ResolvePropertyObjectSchema(JsonElement root, string propertyName)
    {
        var propertySchema = root.GetProperty("properties").GetProperty(propertyName);
        return ResolveObjectSchema(root, propertySchema);
    }

    private static JsonElement ResolveObjectSchema(JsonElement root, JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            return ResolveReference(root, reference.GetString());
        }

        if (schema.TryGetProperty("anyOf", out var alternatives))
        {
            foreach (var alternative in alternatives.EnumerateArray())
            {
                if (alternative.TryGetProperty("$ref", out reference))
                {
                    return ResolveReference(root, reference.GetString());
                }

                if (alternative.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && type.GetString() != "null")
                {
                    return alternative;
                }
            }
        }

        return schema;
    }

    private static JsonElement ResolveReference(JsonElement root, [NotNull] string? reference)
    {
        reference.Should().StartWith("#/$defs/");
        var definitionName = reference["#/$defs/".Length..]
            .Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

        return root.GetProperty("$defs").GetProperty(definitionName);
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record SelectorSchemaRequest
    {
        public WorkspaceSelector? Workspace { get; init; }

        public ProjectSelector? Project { get; init; }

        public DocumentSelector? Document { get; init; }

        public LocationSelector? Location { get; init; }

        public SymbolSelector? Symbol { get; init; }

        public ScopeSelector? Scope { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record ProjectCollectionSchemaRequest
    {
        public IReadOnlyList<ProjectSelector>? Projects { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record ProjectDictionarySchemaRequest
    {
        public IReadOnlyDictionary<string, ProjectSelector>? Projects { get; init; }
    }
}
