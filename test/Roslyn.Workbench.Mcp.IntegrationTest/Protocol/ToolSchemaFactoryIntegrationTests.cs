using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Roslyn.Workbench.Mcp.CodeActions.Contracts;
using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Transaction.Contracts;

namespace Roslyn.Workbench.Mcp.IntegrationTest.Protocol;

public sealed class ToolSchemaFactoryIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_FixedContractLimits_WHEN_ExportingInputSchemas_THEN_ShouldPublishDeclaredDefaults()
    {
        var target = CreateTarget();

        var calleesSchema = target.CreateInputSchema<FindCalleesRequest>();
        var operationTreeSchema = target.CreateInputSchema<GetOperationTreeRequest>();
        var controlFlowGraphSchema = target.CreateInputSchema<GetControlFlowGraphRequest>();
        var duplicateCodeSchema = target.CreateInputSchema<FindDuplicateCodeRequest>();
        var derivedTypesSchema = target.CreateInputSchema<FindDerivedTypesRequest>();
        var typeHierarchySchema = target.CreateInputSchema<GetTypeHierarchyRequest>();
        var codeContextSchema = target.CreateInputSchema<GetCodeContextRequest>();
        var transactionPreviewSchema = target.CreateInputSchema<TransactionPreviewRequest>();
        var fixAllSchema = target.CreateInputSchema<StageFixAllRequest>();

        GetProperty(calleesSchema, "maxDepth").GetProperty("default").GetInt32().Should().Be(3);
        GetProperty(operationTreeSchema, "maxDepth").GetProperty("default").GetInt32().Should().Be(8);
        GetProperty(controlFlowGraphSchema, "maxBlocks").GetProperty("default").GetInt32().Should().Be(64);
        GetProperty(controlFlowGraphSchema, "maxRegions").GetProperty("default").GetInt32().Should().Be(32);
        GetProperty(duplicateCodeSchema, "minimumStatements").GetProperty("default").GetInt32().Should().Be(3);
        GetProperty(derivedTypesSchema, "maxDepth").GetProperty("default").GetInt32().Should().Be(3);
        GetProperty(typeHierarchySchema, "maxDepth").GetProperty("default").GetInt32().Should().Be(3);
        GetProperty(codeContextSchema, "beforeLines").GetProperty("default").GetInt32().Should().Be(10);
        GetProperty(codeContextSchema, "afterLines").GetProperty("default").GetInt32().Should().Be(10);
        GetProperty(transactionPreviewSchema, "contextLines").GetProperty("default").GetInt32().Should().Be(3);
        GetProperty(fixAllSchema, "maxChanges").GetProperty("default").GetInt32().Should().Be(50);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_CuratedResultLimit_WHEN_ExportingInputSchema_THEN_ShouldPublishIntegerDefault()
    {
        var target = CreateTarget();

        var result = target.CreateInputSchema<FindCalleesRequest>();

        var limitProperty = GetProperty(result, "calleesLimit");
        limitProperty.GetProperty("default").GetInt32().Should().Be(100);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_DocumentRequest_WHEN_ExportingInputSchema_THEN_ShouldPublishProjectQualifier()
    {
        var target = CreateTarget();

        var result = target.CreateInputSchema<FormatDocumentRequest>();

        var documentProperty = GetProperty(result, "document");
        var projectProperty = GetProperty(documentProperty, "project");
        GetProperty(projectProperty, "projectId").ValueKind.Should().NotBe(JsonValueKind.Undefined);
        GetProperty(projectProperty, "name").ValueKind.Should().NotBe(JsonValueKind.Undefined);
        GetProperty(projectProperty, "path").ValueKind.Should().NotBe(JsonValueKind.Undefined);
        GetProperty(projectProperty, "targetFramework").ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CodeActionTargetSelectors_WHEN_ExportingInputSchemas_THEN_ShouldPublishRequiredNonNullableProperties()
    {
        var target = CreateTarget();
        var schemaMethod = typeof(ToolSchemaFactory).GetMethod(nameof(ToolSchemaFactory.CreateInputSchema))
            ?? throw new InvalidOperationException("The input-schema factory method was not found.");

        var requestTypes = typeof(StageFixAllRequest).Assembly
            .GetTypes()
            .Where(static type => type.Name.EndsWith("Request", StringComparison.Ordinal)
                && type.Namespace?.Contains(".Contracts", StringComparison.Ordinal) == true)
            .ToArray();

        var targetSelectorProperties = requestTypes
            .SelectMany(static type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Where(IsTargetSelectorProperty)
            .ToArray();

        targetSelectorProperties.Should().HaveCount(20);
        foreach (var targetSelectorProperty in targetSelectorProperties)
        {
            var declaringType = targetSelectorProperty.DeclaringType
                ?? throw new InvalidOperationException("The target selector property did not have a declaring type.");

            var closedSchemaMethod = schemaMethod.MakeGenericMethod(declaringType);
            var publishedSchema = closedSchemaMethod.Invoke(target, null) is JsonElement schema
                ? schema
                : throw new InvalidOperationException("The input-schema factory did not return a JSON element.");

            var jsonPropertyName = JsonNamingPolicy.CamelCase.ConvertName(targetSelectorProperty.Name);
            var requiredProperties = publishedSchema.GetProperty("required")
                .EnumerateArray()
                .Select(static item => item.GetString())
                .ToArray();

            requiredProperties.Should().Contain(jsonPropertyName);
            AllowsNull(GetProperty(publishedSchema, jsonPropertyName)).Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_BuiltInToolRequests_WHEN_AuditingLimitProperties_THEN_EveryLimitShouldDeclareAndPublishItsDefault()
    {
        var target = CreateTarget();
        var schemaMethod = typeof(ToolSchemaFactory).GetMethod(nameof(ToolSchemaFactory.CreateInputSchema))
            ?? throw new InvalidOperationException("The input-schema factory method was not found.");

        var requestAssemblies = new[]
        {
            typeof(TransactionPreviewRequest).Assembly,
            typeof(StageFixAllRequest).Assembly,
            typeof(FindCalleesRequest).Assembly,
        };

        var requestTypes = requestAssemblies
            .Distinct()
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.Name.EndsWith("Request", StringComparison.Ordinal)
                && type.Namespace?.Contains(".Contracts", StringComparison.Ordinal) == true)
            .ToArray();

        var limitProperties = requestTypes
            .SelectMany(static type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Where(IsLimitProperty)
            .ToArray();

        limitProperties.Should().HaveCount(46);
        foreach (var limitProperty in limitProperties)
        {
            var declaringType = limitProperty.DeclaringType
                ?? throw new InvalidOperationException("The limit property did not have a declaring type.");

            var closedSchemaMethod = schemaMethod.MakeGenericMethod(declaringType);
            var publishedSchema = closedSchemaMethod.Invoke(target, null) is JsonElement schema
                ? schema
                : throw new InvalidOperationException("The input-schema factory did not return a JSON element.");

            var jsonPropertyName = JsonNamingPolicy.CamelCase.ConvertName(limitProperty.Name);
            var publishedDefault = GetProperty(publishedSchema, jsonPropertyName).GetProperty("default");

            var fixedDefault = limitProperty.GetCustomAttribute<DefaultValueAttribute>()
                ?? throw new InvalidOperationException($"{declaringType.Name}.{limitProperty.Name} must declare its fixed default.");

            publishedDefault.GetInt32().Should().Be(Convert.ToInt32(fixedDefault.Value, System.Globalization.CultureInfo.InvariantCulture));
            var defaultRequest = Activator.CreateInstance(declaringType, nonPublic: true)
                ?? throw new InvalidOperationException($"{declaringType.Name} could not be constructed for its default-value audit.");

            limitProperty.GetValue(defaultRequest).Should().Be(fixedDefault.Value);
        }
    }

    private static ToolSchemaFactory CreateTarget()
    {
        return new ToolSchemaFactory(new McpSdkSchemaProvider());
    }

    private static bool IsLimitProperty(PropertyInfo property)
    {
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return propertyType == typeof(int)
            && (property.Name.StartsWith("Max", StringComparison.Ordinal)
                || property.Name.StartsWith("Minimum", StringComparison.Ordinal)
                || property.Name.EndsWith("Lines", StringComparison.Ordinal)
                || property.Name.EndsWith("Limit", StringComparison.Ordinal));
    }

    private static bool IsTargetSelectorProperty(PropertyInfo property)
    {
        return property.PropertyType == typeof(LocationSelector)
            || property.PropertyType == typeof(SymbolSelector)
            || property.PropertyType == typeof(ScopeSelector);
    }

    private static bool AllowsNull(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return false;
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return string.Equals(type.GetString(), "null", StringComparison.Ordinal);
        }

        return type.ValueKind == JsonValueKind.Array
            && type.EnumerateArray().Any(static item => string.Equals(item.GetString(), "null", StringComparison.Ordinal));
    }

    private static JsonElement GetProperty(JsonElement schema, string propertyName)
    {
        return schema.GetProperty("properties").GetProperty(propertyName);
    }

}
