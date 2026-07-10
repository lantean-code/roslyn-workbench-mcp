using System.Reflection;
using System.Text.Json;

using Roslyn.Workbench.Mcp.Test.Contracts.Schema;

namespace Roslyn.Workbench.Mcp.Test.Contracts.Inspection;

[Trait("Category", "Contract")]
public sealed class GetControlFlowGraphRequestSchemaTests
{
    [Fact]
    public void GIVEN_GetControlFlowGraphRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishBoundedGraphProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.GetControlFlowGraph), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");

        requestProperties.TryGetProperty("symbol", out var symbolProperty).Should().BeTrue();
        requestProperties.TryGetProperty("location", out var locationProperty).Should().BeTrue();
        requestProperties.TryGetProperty("maxBlocks", out var maxBlocksProperty).Should().BeTrue();
        requestProperties.TryGetProperty("maxRegions", out var maxRegionsProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        symbolProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        locationProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        maxBlocksProperty.GetProperty("type").GetString().Should().Be("integer");
        maxRegionsProperty.GetProperty("type").GetString().Should().Be("integer");
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }
}
