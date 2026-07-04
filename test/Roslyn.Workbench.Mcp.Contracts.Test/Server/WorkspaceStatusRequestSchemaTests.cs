using System.Reflection;
using System.Text.Json;

using Roslyn.Workbench.Mcp.Contracts.Test.Schema;

namespace Roslyn.Workbench.Mcp.Contracts.Test.Server;

public sealed class WorkspaceStatusRequestSchemaTests
{
    [Fact]
    public void GIVEN_WorkspaceStatusRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishWorkspaceSelectorAndDetailProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.WorkspaceStatus), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");

        requestProperties.TryGetProperty("workspace", out var workspaceProperty).Should().BeTrue();
        requestProperties.TryGetProperty("detail", out var detailProperty).Should().BeTrue();
        workspaceProperty.GetRawText().Should().Contain("workspaceId");
        workspaceProperty.GetRawText().Should().Contain("alias");
        workspaceProperty.GetRawText().Should().Contain("path");
        detailProperty.GetRawText().Should().Contain("Minimal");
        detailProperty.GetRawText().Should().Contain("Standard");
        detailProperty.GetRawText().Should().Contain("Full");
    }
}
