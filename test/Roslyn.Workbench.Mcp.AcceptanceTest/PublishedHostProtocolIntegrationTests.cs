namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedHostProtocolIntegrationTests
{
    private static readonly TimeSpan _signalTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task GIVEN_PublishedHost_WHEN_UsingStdioProtocol_THEN_ShouldPublishExpectedCatalogueAndStatus()
    {
        var executablePath = PublishedHostExecutable.ResolveFromEnvironment();
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            var toolNames = tools.Select(static tool => tool.Name).ToArray();

            toolNames.Should().Contain("server-status");
            toolNames.Should().Contain("search-symbols");
            toolNames.Should().Contain("list-code-actions");
            var findCallees = tools.Single(static tool => tool.Name == "find-callees");
            var findCalleesProperties = findCallees.ProtocolTool.InputSchema.GetProperty("properties");

            findCalleesProperties.GetProperty("maxDepth").GetProperty("default").GetInt32().Should().Be(3);
            findCalleesProperties
                .GetProperty("calleesLimit")
                .GetProperty("default")
                .GetInt32()
                .Should()
                .Be(100);

            var statusResult = await target.CallToolAsync(
                "server-status",
                new Dictionary<string, object?>
                {
                    ["detail"] = "Full",
                },
                TestContext.Current.CancellationToken);

            statusResult.IsError.Should().NotBeTrue();
            statusResult.StructuredContent.Should().NotBeNull();

            var status = AcceptanceProtocol.GetSuccessData(statusResult);
            var serverVersion = status.GetProperty("serverVersion").GetString();
            serverVersion.Should().NotBeNullOrWhiteSpace();
            status.GetProperty("roslynVersion").GetString().Should().NotBeNullOrWhiteSpace();
            status.GetProperty("toolCount").GetInt32().Should().Be(toolNames.Length);
            status.GetProperty("codeActions").GetProperty("isAvailable").GetBoolean().Should().BeTrue();
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Published Host: {executablePath}{Environment.NewLine}Product version: {serverVersion}");

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

    [Fact]
    public async Task GIVEN_ControlledExternalQuery_WHEN_SendingKnownRequestId_THEN_ShouldCompleteAfterDeterministicRelease()
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
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            tools.Select(static tool => tool.Name).Should().Contain("host-valid-query");
            tools.Select(static tool => tool.Name).Should().Contain("host-valid-mutation");

            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var openResult = await target.CallToolAsync(
                "workspace-open",
                new Dictionary<string, object?>
                {
                    ["path"] = projectPath,
                    ["workspaceRoot"] = target.WorkspaceRoot,
                },
                TestContext.Current.CancellationToken);

            openResult.IsError.Should().NotBeTrue();
            var workspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
            var controlDirectory = Path.Combine(target.ScenarioRoot, "control", "known-request");
            var readyPath = Path.Combine(controlDirectory, "ready");
            var releasePath = Path.Combine(controlDirectory, "release");

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeoutSource.CancelAfter(_signalTimeout);
            var readiness = AcceptanceFileSignal.WaitAsync(readyPath, timeoutSource.Token);
            var invocation = target.StartCancellableToolCall(
                "host-valid-query",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspace.CreateSelector(),
                    ["name"] = "KnownRequest",
                    ["controlDirectory"] = controlDirectory,
                },
                timeoutSource.Token);

            await readiness;
            await File.WriteAllTextAsync(releasePath, string.Empty, timeoutSource.Token);
            var queryResult = await invocation.Completion.WaitAsync(timeoutSource.Token);

            queryResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(queryResult)
                .GetProperty("value")
                .GetString()
                .Should()
                .Be("KnownRequest");
            invocation.RequestId.ToString().Should().Contain("acceptance-");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }
}
