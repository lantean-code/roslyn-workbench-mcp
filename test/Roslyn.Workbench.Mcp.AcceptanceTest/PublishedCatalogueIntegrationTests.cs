namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedCatalogueIntegrationTests
{
    [Fact]
    public async Task GIVEN_WorkspaceLifecycleChanges_WHEN_ListingTools_THEN_ShouldKeepCatalogueAndMetadataStable()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets:
            [
                AcceptancePluginAsset.HostQuery,
                AcceptancePluginAsset.HostMutation,
            ]);

        try
        {
            var initialTools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            AssertRepresentativeMetadata(initialTools);

            var openResult = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"));
            var workspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
            var workspaceSelector = workspace.CreateSelector();

            await target.CallToolAsync(
                "transaction-start",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            var transactionTools = await target.ListToolsAsync(TestContext.Current.CancellationToken);

            await target.CallToolAsync(
                "transaction-rollback",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);
            await target.CallToolAsync(
                "workspace-close",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            var closedTools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            CreateCatalogueFingerprint(transactionTools).Should().Equal(CreateCatalogueFingerprint(initialTools));
            CreateCatalogueFingerprint(closedTools).Should().Equal(CreateCatalogueFingerprint(initialTools));
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_BoundedQuery_WHEN_UsingDefaultZeroAndLowLimits_THEN_ShouldApplyStablePrefixes()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            workspaceAsset: AcceptanceWorkspaceAsset.SolutionHierarchy);

        try
        {
            var openResult = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.slnx"));
            var workspaceSelector = AcceptanceWorkspaceIdentity.FromOpenResult(openResult).CreateSelector();

            var defaultResult = await SearchSymbolsAsync(target, workspaceSelector, limit: null);
            var zeroResult = await SearchSymbolsAsync(target, workspaceSelector, limit: 0);
            var firstLowResult = await SearchSymbolsAsync(target, workspaceSelector, limit: 1);
            var secondLowResult = await SearchSymbolsAsync(target, workspaceSelector, limit: 1);

            var defaultSymbols = GetSymbols(defaultResult);
            var zeroSymbols = GetSymbols(zeroResult);
            var firstLowSymbols = GetSymbols(firstLowResult);
            var secondLowSymbols = GetSymbols(secondLowResult);

            defaultSymbols.GetProperty("items").GetArrayLength().Should().BeGreaterThan(1);
            defaultSymbols.GetProperty("hasMore").GetBoolean().Should().BeFalse();
            zeroSymbols.GetProperty("items").GetArrayLength().Should().Be(0);
            zeroSymbols.GetProperty("hasMore").GetBoolean().Should().BeTrue();
            firstLowSymbols.GetProperty("items").GetArrayLength().Should().Be(1);
            firstLowSymbols.GetProperty("hasMore").GetBoolean().Should().BeTrue();
            firstLowSymbols.GetProperty("items")[0].GetRawText().Should().Be(defaultSymbols.GetProperty("items")[0].GetRawText());
            secondLowSymbols.GetRawText().Should().Be(firstLowSymbols.GetRawText());
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> OpenWorkspaceAsync(
        AcceptanceProcessFixture target,
        string path)
    {
        return await target.CallToolAsync(
            "workspace-open",
            new Dictionary<string, object?>
            {
                ["path"] = path,
                ["workspaceRoot"] = target.WorkspaceRoot,
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> SearchSymbolsAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        int? limit)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["workspace"] = workspaceSelector,
            ["query"] = "App",
        };
        if (limit is not null)
        {
            arguments["symbolsLimit"] = limit.Value;
        }

        return await target.CallToolAsync(
            "search-symbols",
            arguments,
            TestContext.Current.CancellationToken);
    }

    private static System.Text.Json.JsonElement GetSymbols(ModelContextProtocol.Protocol.CallToolResult result)
    {
        result.IsError.Should().NotBeTrue();
        return AcceptanceProtocol.GetSuccessData(result).GetProperty("symbols");
    }

    private static string[] CreateCatalogueFingerprint(IList<ModelContextProtocol.Client.McpClientTool> tools)
    {
        return tools
            .OrderBy(static tool => tool.Name, StringComparer.Ordinal)
            .Select(static tool => $"{tool.Name}|{tool.ProtocolTool.Description}|{tool.ProtocolTool.InputSchema.GetRawText()}")
            .ToArray();
    }

    private static void AssertRepresentativeMetadata(IList<ModelContextProtocol.Client.McpClientTool> tools)
    {
        foreach (var toolName in new[] { "workspace-list", "search-symbols", "rename-symbol", "list-code-actions", "host-valid-query" })
        {
            var tool = tools.Single(item => item.Name == toolName);
            tool.ProtocolTool.Description.Should().NotBeNullOrWhiteSpace();
            tool.ProtocolTool.InputSchema.GetProperty("type").GetString().Should().Be("object");
            tool.ProtocolTool.Annotations.Should().NotBeNull();
            tool.ProtocolTool.Annotations!.Title.Should().NotBeNullOrWhiteSpace();
        }
    }
}
