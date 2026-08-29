using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;
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
    public void GIVEN_RequestsWithCrossPropertyRules_WHEN_ExportingInputSchemas_THEN_ShouldExplainValidCombinations()
    {
        var findCalleesSchema = _target.GetInputSchema<FindCalleesRequest>();
        var controlFlowGraphSchema = _target.GetInputSchema<GetControlFlowGraphRequest>();
        var searchSymbolsSchema = _target.GetInputSchema<SearchSymbolsRequest>();

        GetDescription(findCalleesSchema, "symbol").Should().Contain("exactly one of symbol or location");
        GetDescription(findCalleesSchema, "location").Should().Contain("exactly one of location or symbol");
        GetDescription(controlFlowGraphSchema, "symbol").Should().Contain("exactly one of symbol or location");
        GetDescription(controlFlowGraphSchema, "location").Should().Contain("exactly one of location or symbol");
        GetDescription(searchSymbolsSchema, "query").Should().Contain("provide query, metadataName, or both");
        GetDescription(searchSymbolsSchema, "metadataName").Should().Contain("provide metadataName, query, or both");
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

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CoreContractTypes_WHEN_AuditingSerializedProperties_THEN_ShouldDescribeEveryProperty()
    {
        var missingDescriptions = typeof(FindCalleesRequest).Assembly
            .GetTypes()
            .Where(static type => type.Namespace?.StartsWith("Roslyn.Workbench.Mcp.Plugins.Core.Contracts.", StringComparison.Ordinal) == true)
            .SelectMany(static type => type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public))
            .Where(IsSerializedContractProperty)
            .Where(static property => string.IsNullOrWhiteSpace(property.GetCustomAttribute<DescriptionAttribute>()?.Description))
            .Select(static property => $"{property.DeclaringType!.FullName}.{property.Name}")
            .ToArray();

        missingDescriptions.Should().BeEmpty();
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

    private static bool IsSerializedContractProperty(PropertyInfo property)
    {
        return property.GetIndexParameters().Length == 0 && property.GetCustomAttribute<JsonIgnoreAttribute>() is null;
    }
}
