using System.Text.Json;

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

    [Fact]
    public async Task GIVEN_PublishedExternalPlugin_WHEN_UsingInvocationCache_THEN_ShouldReuseAndRejectNonAdmittedValues()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets: [AcceptancePluginAsset.HostQuery]);

        try
        {
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var openArguments = new Dictionary<string, object?>
            {
                ["path"] = projectPath,
                ["workspaceRoot"] = target.WorkspaceRoot,
            };

            var openResult = await target.CallToolAsync(
                "workspace-open",
                openArguments,
                TestContext.Current.CancellationToken);

            var workspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);

            var firstCached = await InvokeCacheCalibrationAsync(
                target,
                workspace,
                "AcceptanceReuse",
                TestContext.Current.CancellationToken);
            var secondCached = await InvokeCacheCalibrationAsync(
                target,
                workspace,
                "AcceptanceReuse",
                TestContext.Current.CancellationToken);
            var firstNull = await InvokeCacheCalibrationAsync(
                target,
                workspace,
                "AcceptanceNull",
                TestContext.Current.CancellationToken,
                returnNull: true);
            var secondNull = await InvokeCacheCalibrationAsync(
                target,
                workspace,
                "AcceptanceNull",
                TestContext.Current.CancellationToken,
                returnNull: true);
            var firstDisposable = await InvokeCacheCalibrationAsync(
                target,
                workspace,
                "AcceptanceDisposable",
                TestContext.Current.CancellationToken,
                returnDisposable: true);
            var secondDisposable = await InvokeCacheCalibrationAsync(
                target,
                workspace,
                "AcceptanceDisposable",
                TestContext.Current.CancellationToken,
                returnDisposable: true);
            var firstCoalescedTask = InvokeCacheCalibrationAsync(
                target,
                workspace,
                "AcceptanceCoalescing",
                TestContext.Current.CancellationToken,
                returnNull: true,
                factoryDelayMilliseconds: 100);
            var secondCoalescedTask = InvokeCacheCalibrationAsync(
                target,
                workspace,
                "AcceptanceCoalescing",
                TestContext.Current.CancellationToken,
                returnNull: true,
                factoryDelayMilliseconds: 100);
            var coalesced = await Task.WhenAll(firstCoalescedTask, secondCoalescedTask);

            firstCached.GetProperty("factoryExecutionCount").GetInt32().Should().Be(1);
            secondCached.GetProperty("factoryExecutionCount").GetInt32().Should().Be(1);
            firstCached.GetProperty("payloadLength").GetInt32().Should().Be(1024);
            firstNull.GetProperty("factoryExecutionCount").GetInt32().Should().Be(1);
            secondNull.GetProperty("factoryExecutionCount").GetInt32().Should().Be(2);
            firstDisposable.GetProperty("factoryExecutionCount").GetInt32().Should().Be(1);
            secondDisposable.GetProperty("factoryExecutionCount").GetInt32().Should().Be(2);
            coalesced.Should().OnlyContain(
                result => result.GetProperty("factoryExecutionCount").GetInt32() == 1);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<JsonElement> InvokeCacheCalibrationAsync(
        AcceptanceProcessFixture target,
        AcceptanceWorkspaceIdentity workspace,
        string workload,
        CancellationToken cancellationToken,
        bool returnNull = false,
        bool returnDisposable = false,
        int factoryDelayMilliseconds = 0)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["workspace"] = workspace.CreateSelector(),
            ["workload"] = workload,
            ["keyCount"] = 1,
            ["payloadLength"] = 1024,
            ["returnNull"] = returnNull,
            ["returnDisposable"] = returnDisposable,
            ["factoryDelayMilliseconds"] = factoryDelayMilliseconds,
            ["includeFactoryExecutionCount"] = true,
        };

        var result = await target.CallToolAsync(
            "host-query-cache-calibration",
            arguments,
            cancellationToken);

        result.IsError.Should().NotBeTrue();
        return AcceptanceProtocol.GetSuccessData(result);
    }
}
