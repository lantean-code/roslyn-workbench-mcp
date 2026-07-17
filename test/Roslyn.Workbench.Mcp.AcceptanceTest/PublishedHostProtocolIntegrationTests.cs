namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedHostProtocolIntegrationTests
{
    [Fact]
    public async Task GIVEN_PublishedHost_WHEN_UsingStdioProtocol_THEN_ShouldPublishExpectedCatalogueAndStatus()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            var toolNames = tools.Select(static tool => tool.Name).ToArray();

            toolNames.Should().Contain("server-status");
            toolNames.Should().Contain("search-symbols");
            toolNames.Should().Contain("list-code-actions");

            var statusResult = await target.CallToolAsync(
                "server-status",
                new Dictionary<string, object?>
                {
                    ["detail"] = "Full",
                },
                TestContext.Current.CancellationToken);

            statusResult.IsError.Should().NotBeTrue();
            statusResult.StructuredContent.Should().NotBeNull();

            var status = statusResult.StructuredContent!.Value.GetProperty("data");
            status.GetProperty("serverVersion").GetString().Should().NotBeNullOrWhiteSpace();
            status.GetProperty("roslynVersion").GetString().Should().NotBeNullOrWhiteSpace();
            status.GetProperty("toolCount").GetInt32().Should().Be(toolNames.Length);
            status.GetProperty("codeActions").GetProperty("isAvailable").GetBoolean().Should().BeTrue();

            var pluginIds = status
                .GetProperty("plugins")
                .EnumerateArray()
                .Select(static plugin => plugin.GetProperty("pluginId").GetString())
                .ToArray();

            pluginIds.Should().Contain("roslyn.workbench.core");
            pluginIds.Should().NotContain("roslyn.workbench.codeactions");

            var completion = await target.StopAsync();

            completion.ProcessId.Should().NotBeNull();
            completion.ExitCode.Should().NotBeNull();
            completion.Exception.Should().BeNull();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }
}
