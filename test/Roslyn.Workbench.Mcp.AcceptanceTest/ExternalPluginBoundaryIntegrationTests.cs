namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class ExternalPluginBoundaryIntegrationTests
{
    [Fact]
    public async Task GIVEN_ValidInvalidAndThrowingPackages_WHEN_StartingHost_THEN_ShouldIsolateFailuresAndSanitiseDiagnostics()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets:
            [
                AcceptancePluginAsset.HostQuery,
                AcceptancePluginAsset.HostMutation,
                AcceptancePluginAsset.Invalid,
                AcceptancePluginAsset.Throwing,
            ]);

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            tools.Select(static tool => tool.Name).Should().Contain("host-valid-query");
            tools.Select(static tool => tool.Name).Should().Contain("host-valid-mutation");

            var status = await GetFullStatusAsync(target);
            var plugins = status.GetProperty("plugins").EnumerateArray().ToArray();
            plugins.Should().Contain(plugin =>
                plugin.GetProperty("pluginId").GetString() == "host.valid.query"
                && plugin.GetProperty("enabled").GetBoolean());
            plugins.Should().Contain(plugin =>
                plugin.GetProperty("pluginId").GetString() == "host.valid.mutation"
                && plugin.GetProperty("enabled").GetBoolean());
            plugins.Should().Contain(plugin =>
                !plugin.GetProperty("enabled").GetBoolean()
                && plugin.GetProperty("diagnostics").GetArrayLength() > 0);

            var throwingPlugin = plugins.Single(plugin =>
                plugin.GetProperty("pluginId").GetString() == "test.throwing.configuration");
            var throwingDiagnostics = throwingPlugin.GetProperty("diagnostics").GetRawText();
            throwingDiagnostics.Should().Contain("PluginLoad");
            throwingDiagnostics.Should().Contain(nameof(InvalidOperationException));
            throwingDiagnostics.Should().NotContain("Configuration failed");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_DuplicatePluginIds_WHEN_StartingHost_THEN_ShouldDisableEveryCollidingPackage()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets:
            [
                AcceptancePluginAsset.HostQuery,
                AcceptancePluginAsset.HostQueryDuplicate,
            ]);

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            tools.Select(static tool => tool.Name).Should().NotContain("host-valid-query");

            var status = await GetFullStatusAsync(target);
            var collisions = status
                .GetProperty("plugins")
                .EnumerateArray()
                .Where(plugin => plugin.GetProperty("pluginId").GetString() == "host.valid.query")
                .ToArray();

            collisions.Should().HaveCount(2);
            collisions.Should().OnlyContain(plugin =>
                !plugin.GetProperty("enabled").GetBoolean()
                && plugin.GetProperty("diagnostics").GetRawText().Contains("PluginCollision", StringComparison.Ordinal)
                && plugin.GetProperty("diagnostics").GetRawText().Contains("same plugin ID", StringComparison.Ordinal));
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_UnsupportedPluginTransportContracts_WHEN_StartingHost_THEN_ShouldDisablePluginWithoutAffectingCatalogue()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: ["--tool-output-schema-mode", "Full"],
            pluginAssets:
            [
                AcceptancePluginAsset.HostQuery,
                AcceptancePluginAsset.UnsupportedSchema,
            ]);

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            tools.Select(static tool => tool.Name).Should().Contain("host-valid-query");
            tools.Select(static tool => tool.Name).Should().NotContain("unsupported-request-schema");
            tools.Select(static tool => tool.Name).Should().NotContain("unsupported-response-schema");

            var status = await GetFullStatusAsync(target);
            var plugin = status
                .GetProperty("plugins")
                .EnumerateArray()
                .Single(item => item.GetProperty("pluginId").GetString() == "test.unsupported.schema");
            var diagnostics = plugin.GetProperty("diagnostics").GetRawText();

            plugin.GetProperty("enabled").GetBoolean().Should().BeFalse();
            diagnostics.Should().Contain("PluginToolSchema");
            diagnostics.Should().Contain("request contract");
            diagnostics.Should().Contain("response contract");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_PackageAddedAfterInitialisation_WHEN_RestartingHost_THEN_ShouldDiscoverItOnlyAfterRestart()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets: [AcceptancePluginAsset.HostQuery]);

        try
        {
            var initialTools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            initialTools.Select(static tool => tool.Name).Should().Contain("host-valid-query");
            initialTools.Select(static tool => tool.Name).Should().NotContain("host-valid-mutation");

            target.InstallPluginAsset(AcceptancePluginAsset.HostMutation);
            var unchangedTools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            unchangedTools.Select(static tool => tool.Name).Should().NotContain("host-valid-mutation");

            await target.RestartAsync(TestContext.Current.CancellationToken);
            var restartedTools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            restartedTools.Select(static tool => tool.Name).Should().Contain("host-valid-mutation");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<System.Text.Json.JsonElement> GetFullStatusAsync(AcceptanceProcessFixture target)
    {
        var result = await target.CallToolAsync(
            "server-status",
            new Dictionary<string, object?>
            {
                ["detail"] = "Full",
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().NotBeTrue();
        return AcceptanceProtocol.GetSuccessData(result);
    }
}
