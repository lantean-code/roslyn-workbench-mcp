namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedDistributionIntegrationTests
{
    [Fact]
    public async Task GIVEN_EnvironmentAndRepeatedArguments_WHEN_ReadingFullStatus_THEN_ShouldUseLastArgumentAndOmitSensitivePaths()
    {
        var environmentVariables = new Dictionary<string, string?>
        {
            ["ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS"] = "5",
        };
        var arguments = new[]
        {
            "--default-max-results=7",
            "--default-max-results=9",
        };

        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: arguments,
            pluginAssets: [AcceptancePluginAsset.HostQuery],
            environmentVariables: environmentVariables);

        try
        {
            var statusResult = await target.CallToolAsync(
                "server-status",
                new Dictionary<string, object?>
                {
                    ["detail"] = "Full",
                },
                TestContext.Current.CancellationToken);

            statusResult.IsError.Should().NotBeTrue();
            var status = AcceptanceProtocol.GetSuccessData(statusResult);
            var configuration = status.GetProperty("configuration");
            configuration.GetProperty("defaultMaxResults").GetInt32().Should().Be(9);
            configuration.GetProperty("toolOutputSchemaMode").GetString().Should().Be("Omit");

            var serializedStatus = status.GetRawText();
            serializedStatus.Should().NotContain(target.StateRoot);
            serializedStatus.Should().NotContain(target.PluginRoot);
            serializedStatus.Should().NotContain(Path.GetFileName(target.ScenarioRoot));

            var msBuild = status.GetProperty("msBuild");
            if (msBuild.GetProperty("isAvailable").GetBoolean())
            {
                msBuild.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
            }
            else
            {
                msBuild.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
            }
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_DefaultAndFullSchemaModes_WHEN_ListingTools_THEN_ShouldPublishSchemasOnlyInFullMode()
    {
        await using var defaultTarget = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets:
            [
                AcceptancePluginAsset.HostQuery,
                AcceptancePluginAsset.HostMutation,
            ]);
        await using var fullTarget = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: ["--tool-output-schema-mode=Full"],
            pluginAssets:
            [
                AcceptancePluginAsset.HostQuery,
                AcceptancePluginAsset.HostMutation,
            ]);

        try
        {
            var defaultTools = await defaultTarget.ListToolsAsync(TestContext.Current.CancellationToken);
            var fullTools = await fullTarget.ListToolsAsync(TestContext.Current.CancellationToken);
            var representativeToolNames = new[]
            {
                "workspace-list",
                "search-symbols",
                "rename-symbol",
                "list-code-actions",
                "host-valid-query",
                "host-valid-mutation",
            };

            foreach (var toolName in representativeToolNames)
            {
                defaultTools.Single(tool => tool.Name == toolName).ProtocolTool.OutputSchema.Should().BeNull();
                var fullTool = fullTools.Single(tool => tool.Name == toolName);
                fullTool.ProtocolTool.OutputSchema.Should().NotBeNull();
                fullTool.ProtocolTool.OutputSchema!.Value.GetProperty("type").GetString().Should().Be("object");
            }
        }
        catch
        {
            defaultTarget.RetainRootOnFailure();
            fullTarget.RetainRootOnFailure();
            throw;
        }
    }
}
