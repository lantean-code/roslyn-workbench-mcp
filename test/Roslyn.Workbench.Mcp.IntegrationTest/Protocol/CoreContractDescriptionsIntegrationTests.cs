using System.Reflection;
using System.Text.Json;
using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Plugins.Registration;
using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class CoreContractDescriptionsIntegrationTests
{
    private readonly McpSdkSchemaProvider _target;

    public CoreContractDescriptionsIntegrationTests()
    {
        _target = new McpSdkSchemaProvider();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CoreRequestContract_WHEN_ExportingInputSchema_THEN_ShouldPublishPropertyDescription()
    {
        var schema = _target.GetInputSchema<FindCalleesRequest>();

        schema.GetProperty("properties").GetProperty("maxDepth").GetProperty("description").GetString()
            .Should()
            .Be("The maximum call depth to traverse when indirect callees are included. Direct callees are at depth one.");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_RequestsWithCrossMemberValidation_WHEN_ExportingInputSchemas_THEN_ShouldPublishRuleOnceAndRetainPropertyPurpose()
    {
        var findCalleesSchema = _target.GetInputSchema<FindCalleesRequest>();
        var controlFlowGraphSchema = _target.GetInputSchema<GetControlFlowGraphRequest>();
        var searchSymbolsSchema = _target.GetInputSchema<SearchSymbolsRequest>();

        findCalleesSchema.GetProperty("description").GetString().Should().Be("Provide exactly one of symbol or location.");
        GetDescription(findCalleesSchema, "symbol").Should().Be("Symbol whose callees should be found.");
        GetDescription(findCalleesSchema, "location").Should().Be("Source location or executable region whose contained callees should be found.");
        controlFlowGraphSchema.GetProperty("description").GetString().Should().Be("Provide exactly one of symbol or location.");
        GetDescription(controlFlowGraphSchema, "symbol").Should().Be("Symbol whose control-flow graph should be returned.");
        GetDescription(controlFlowGraphSchema, "location").Should().Be("Source location whose enclosing executable body should be graphed.");
        searchSymbolsSchema.GetProperty("description").GetString().Should().Be("Provide query, metadataName, or both.");
        GetDescription(searchSymbolsSchema, "query").Should().Be("Source-name query.");
        searchSymbolsSchema.GetProperty("properties").GetProperty("metadataName").TryGetProperty("description", out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_RequestWithRootGuidance_WHEN_PublishingPluginTool_THEN_ShouldCopyGuidanceIntoToolDescription()
    {
        var target = new McpToolProtocolFactory(new ToolSchemaFactory(new McpSdkSchemaProvider()));
        var registeredTool = new RegisteredTool
        {
            Metadata = new ToolRegistrationMetadata
            {
                Name = "search-symbols",
                Title = "Search Symbols",
                Description = "Searches declarations.",
            },
            Kind = ToolKind.Query,
            ResponseType = typeof(SymbolSearchData),
        };

        var result = target.CreatePluginTool<SearchSymbolsRequest>(registeredTool, ToolOutputSchemaMode.Omit);

        result.Description.Should().Be("Searches declarations. Input: Provide query, metadataName, or both.");
        result.InputSchema.GetProperty("description").GetString().Should().Be("Provide query, metadataName, or both.");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_RequestsWithCrossPropertyRules_WHEN_AuditingValidationMetadata_THEN_ShouldDeclareThoseRules()
    {
        var findCalleesRule = GetContractAttribute<RequiresExactlyOneAttribute, FindCalleesRequest>();
        var controlFlowGraphRule = GetContractAttribute<RequiresExactlyOneAttribute, GetControlFlowGraphRequest>();
        var searchSymbolsRule = GetContractAttribute<RequiresAtLeastOneAttribute, SearchSymbolsRequest>();

        findCalleesRule.MemberNames.Should().Equal(nameof(FindCalleesRequest.Symbol), nameof(FindCalleesRequest.Location));
        controlFlowGraphRule.MemberNames.Should().Equal(nameof(GetControlFlowGraphRequest.Symbol), nameof(GetControlFlowGraphRequest.Location));
        searchSymbolsRule.MemberNames.Should().Equal(nameof(SearchSymbolsRequest.Query), nameof(SearchSymbolsRequest.MetadataName));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CoreResponseContract_WHEN_ExportingValueSchema_THEN_ShouldPublishPropertyDescription()
    {
        var schema = _target.GetValueSchema<AsyncAnalysisData>();

        schema.GetProperty("properties").GetProperty("findings").GetProperty("description").GetString()
            .Should()
            .Be("The returned findings.");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CoreNestedResponseContract_WHEN_ExportingValueSchema_THEN_ShouldPublishNestedPropertyDescription()
    {
        var schema = _target.GetValueSchema<OperationTreeData>();

        var childrenSchema = FindPropertySchema(schema, "children");

        childrenSchema.GetProperty("description").GetString().Should().Be("The projected child operations.");
    }

    private static JsonElement FindPropertySchema(JsonElement schema, string propertyName)
    {
        if (schema.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("properties", out var properties) && properties.TryGetProperty(propertyName, out var propertySchema))
            {
                return propertySchema;
            }

            foreach (var property in schema.EnumerateObject())
            {
                var foundSchema = FindPropertySchema(property.Value, propertyName);
                if (foundSchema.ValueKind != JsonValueKind.Undefined)
                {
                    return foundSchema;
                }
            }
        }
        else if (schema.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in schema.EnumerateArray())
            {
                var foundSchema = FindPropertySchema(item, propertyName);
                if (foundSchema.ValueKind != JsonValueKind.Undefined)
                {
                    return foundSchema;
                }
            }
        }

        return default;
    }

    private static string? GetDescription(JsonElement schema, string propertyName)
    {
        return schema.GetProperty("properties").GetProperty(propertyName).GetProperty("description").GetString();
    }

    private static TAttribute GetContractAttribute<TAttribute, TContract>()
        where TAttribute : Attribute
    {
        return typeof(TContract).GetCustomAttribute<TAttribute>()
            ?? throw new InvalidOperationException($"{typeof(TContract).Name} does not declare {typeof(TAttribute).Name}.");
    }
}
