using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Roslyn.Workbench.Mcp.CodeActions.Contracts;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class CodeActionPropertyDescriptionsIntegrationTests
{
    private readonly McpSdkSchemaProvider _target;

    public CodeActionPropertyDescriptionsIntegrationTests()
    {
        _target = new McpSdkSchemaProvider();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CodeActionRequestContract_WHEN_ExportingInputSchema_THEN_ShouldPublishPropertyDescriptions()
    {
        var schema = _target.GetInputSchema<ListCodeActionsRequest>();
        var properties = schema.GetProperty("properties");

        GetDescription(properties, "document").Should().Be("The target document.");
        GetDescription(properties, "range").Should().Be("The optional selection or caret range. An omitted range selects the complete document.");
        GetDescription(properties, "limit").Should().Be("The maximum number of action leaves to return.");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CodeActionResponseContract_WHEN_ExportingValueSchema_THEN_ShouldPublishNestedPropertyDescriptions()
    {
        var schema = _target.GetValueSchema<CodeActionListItem>();
        var properties = schema.GetProperty("properties");

        GetDescription(properties, "title").Should().Be("The display title.");
        GetDescription(properties, "location").Should().Be("The precise source location to which the action applies.");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_PublishedCodeActionContracts_WHEN_AuditingSerializedProperties_THEN_ShouldDeclareDescriptions()
    {
        var propertiesWithoutDescriptions = typeof(ListCodeActionsRequest).Assembly
            .GetTypes()
            .Where(static type => type.Namespace == "Roslyn.Workbench.Mcp.CodeActions.Contracts" && type.IsClass)
            .SelectMany(static type => type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public))
            .Where(static property => IsSerialized(property))
            .Where(static property => string.IsNullOrWhiteSpace(property.GetCustomAttribute<DescriptionAttribute>()?.Description))
            .Select(static property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToArray();

        propertiesWithoutDescriptions.Should().BeEmpty();
    }

    private static string? GetDescription(JsonElement properties, string propertyName)
    {
        return properties.GetProperty(propertyName).GetProperty("description").GetString();
    }

    private static bool IsSerialized(PropertyInfo property)
    {
        // The audit covers declared properties on published object contracts; inherited request properties are owned by their defining contract assembly, while enums and ICodeActionReferenceRequest do not publish object properties.
        var jsonIgnore = property.GetCustomAttribute<JsonIgnoreAttribute>();
        return jsonIgnore?.Condition != JsonIgnoreCondition.Always;
    }
}
