using System.Text.Json;
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

        GetDescription(properties, "document").Should().Be("Target document.");
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

    private static string? GetDescription(JsonElement properties, string propertyName)
    {
        return properties.GetProperty(propertyName).GetProperty("description").GetString();
    }
}
