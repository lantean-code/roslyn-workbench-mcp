namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class ExternalPluginWorkflowIntegrationTests
{
    [Fact]
    public async Task GIVEN_ExternalPluginPackage_WHEN_StartingPublishedHost_THEN_ShouldDiscoverAndInvokePluginOverStdio()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets: [AcceptancePluginAsset.HostQuery]);

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            var statusResult = await target.CallToolAsync(
                "server-status",
                new Dictionary<string, object?>
                {
                    ["detail"] = "Full",
                },
                TestContext.Current.CancellationToken);

            tools.Select(static tool => tool.Name).Should().Contain("host-valid-query");
            statusResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(statusResult)
                .GetProperty("plugins")
                .EnumerateArray()
                .Should()
                .ContainSingle(plugin =>
                    plugin.GetProperty("pluginId").GetString() == "host.valid.query"
                    && plugin.GetProperty("enabled").GetBoolean());

            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var openResult = await target.CallToolAsync(
                "workspace-open",
                new Dictionary<string, object?>
                {
                    ["path"] = projectPath,
                    ["workspaceRoot"] = target.WorkspaceRoot,
                },
                TestContext.Current.CancellationToken);

            var workspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);

            var queryResult = await target.CallToolAsync(
                "host-valid-query",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspace.CreateSelector(),
                    ["name"] = "Acceptance",
                },
                TestContext.Current.CancellationToken);

            queryResult.IsError.Should().NotBeTrue();
            var queryData = AcceptanceProtocol.GetSuccessData(queryResult);
            queryData.GetProperty("value").GetString().Should().Be("Acceptance");
            queryData.GetProperty("privateDependencyVersion").GetString().Should().NotBeNullOrWhiteSpace();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }
}
