using System.Reflection;

using Roslyn.Workbench.Mcp.Test.Contracts.Schema;

namespace Roslyn.Workbench.Mcp.Test.Contracts.Inspection;

[Trait("Category", "Contract")]
public sealed class NestedQueryBoundsRequestSchemaTests
{
    [Theory]
    [InlineData(nameof(ContractSchemaTestTools.GetSolutionStructure), "documentsPerProjectLimit", 200, int.MaxValue)]
    [InlineData(nameof(ContractSchemaTestTools.GetSolutionStructure), "projectReferencesPerProjectLimit", 50, int.MaxValue)]
    [InlineData(nameof(ContractSchemaTestTools.GetDocumentOutline), "maxDepth", 16, 24)]
    [InlineData(nameof(ContractSchemaTestTools.GetDocumentOutline), "nodesLimit", 200, 2_000)]
    [InlineData(nameof(ContractSchemaTestTools.FindCallers), "callSitesPerCallerLimit", 100, int.MaxValue)]
    [InlineData(nameof(ContractSchemaTestTools.FindDuplicateCode), "occurrencesPerGroupLimit", 100, int.MaxValue)]
    [InlineData(nameof(ContractSchemaTestTools.GetControlFlowGraph), "maxOperationsPerBlock", 32, int.MaxValue)]
    [InlineData(nameof(ContractSchemaTestTools.GetOperationTree), "nodesLimit", 200, 2_000)]
    public void GIVEN_NestedQueryBound_WHEN_GeneratingToolSchema_THEN_ShouldPublishIntegerRangeAndDefault(string methodName, string propertyName, int expectedDefault, int expectedMaximum)
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

        var requestType = method!.GetParameters().Single().ParameterType;
        var schemaProvider = new McpSdkSchemaProvider();
        var requestProperties = schemaProvider.GetInputSchemaForType(requestType).GetProperty("properties");

        requestProperties.TryGetProperty(propertyName, out var property).Should().BeTrue();
        property.GetRawText().Should().Contain("integer");
        property.GetProperty("default").GetInt32().Should().Be(expectedDefault);
        property.GetProperty("minimum").GetInt32().Should().Be(0);
        property.GetProperty("maximum").GetInt32().Should().Be(expectedMaximum);
    }
}
