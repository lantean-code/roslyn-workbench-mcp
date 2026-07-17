using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test.Server.Contracts;

public sealed class ServerStatusDataTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_UnavailableAssemblyVersions_WHEN_Serializing_THEN_ShouldPublishExplicitNullValues()
    {
        var target = new ServerStatusData();

        var result = JsonSerializer.SerializeToElement(target, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        result.GetProperty("serverVersion").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("roslynVersion").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
