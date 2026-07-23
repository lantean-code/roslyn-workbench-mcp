namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class ConcurrencyAndFailureContainmentIntegrationTests
{
    private static readonly TimeSpan _signalTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task GIVEN_HeldQuery_WHEN_CancellingKnownRequest_THEN_ShouldCancelHandlerAndReleaseExclusiveAcquisition()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: ["--max-concurrent-queries=1"],
            pluginAssets: [AcceptancePluginAsset.HostQuery]);

        try
        {
            var workspace = await OpenWorkspaceAsync(
                target,
                Path.Combine(target.WorkspaceRoot, "Sample.csproj"),
                target.WorkspaceRoot);
            var workspaceSelector = workspace.CreateSelector();
            var controlDirectory = Path.Combine(target.ScenarioRoot, "control", "cancellation");
            var readyPath = Path.Combine(controlDirectory, "ready");

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeoutSource.CancelAfter(_signalTimeout);
            var readiness = AcceptanceFileSignal.WaitAsync(readyPath, timeoutSource.Token);
            var invocation = target.StartCancellableToolCall(
                "host-valid-query",
                CreateControlledQueryArguments(workspaceSelector, "Cancelled", controlDirectory),
                timeoutSource.Token);

            await readiness;
            await target.CancelToolCallAsync(invocation.RequestId, timeoutSource.Token);

            var cancelledInvocation = async () => await invocation.Completion;
            await cancelledInvocation.Should().ThrowAsync<OperationCanceledException>();

            var transactionStart = await StartTransactionWhenAvailableAsync(
                target,
                workspaceSelector,
                timeoutSource.Token);
            transactionStart.IsError.Should().NotBeTrue();

            var rollback = await target.CallToolAsync(
                "transaction-rollback",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                timeoutSource.Token);
            rollback.IsError.Should().NotBeTrue();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_OneQuerySlotAndTwoWorkspaces_WHEN_QueryIsHeld_THEN_ShouldRejectSameWorkspaceAndAllowOtherWorkspace()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: ["--max-concurrent-queries=1"],
            pluginAssets: [AcceptancePluginAsset.HostQuery]);

        try
        {
            var secondRoot = target.CopyWorkspaceAsset(AcceptanceWorkspaceAsset.SdkProject, "concurrent-second");
            var firstWorkspace = await OpenWorkspaceAsync(
                target,
                Path.Combine(target.WorkspaceRoot, "Sample.csproj"),
                target.WorkspaceRoot,
                "first");
            var secondWorkspace = await OpenWorkspaceAsync(
                target,
                Path.Combine(secondRoot, "Sample.csproj"),
                secondRoot,
                "second");
            var firstSelector = firstWorkspace.CreateSelector();
            var secondSelector = secondWorkspace.CreateSelector();
            var controlDirectory = Path.Combine(target.ScenarioRoot, "control", "concurrency");
            var readyPath = Path.Combine(controlDirectory, "ready");
            var releasePath = Path.Combine(controlDirectory, "release");

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeoutSource.CancelAfter(_signalTimeout);
            var readiness = AcceptanceFileSignal.WaitAsync(readyPath, timeoutSource.Token);
            var heldInvocation = target.StartCancellableToolCall(
                "host-valid-query",
                CreateControlledQueryArguments(firstSelector, "Held", controlDirectory),
                timeoutSource.Token);

            await readiness;

            var busyQuery = await target.CallToolAsync(
                "host-valid-query",
                new Dictionary<string, object?>
                {
                    ["workspace"] = firstSelector,
                    ["name"] = "Busy",
                },
                timeoutSource.Token);
            AssertWorkspaceBusy(busyQuery);

            var busyTransaction = await target.CallToolAsync(
                "transaction-start",
                new Dictionary<string, object?>
                {
                    ["workspace"] = firstSelector,
                },
                timeoutSource.Token);
            AssertWorkspaceBusy(busyTransaction);

            var otherWorkspaceQuery = await target.CallToolAsync(
                "host-valid-query",
                new Dictionary<string, object?>
                {
                    ["workspace"] = secondSelector,
                    ["name"] = "Independent",
                },
                timeoutSource.Token);
            otherWorkspaceQuery.IsError.Should().NotBeTrue();

            await File.WriteAllTextAsync(releasePath, string.Empty, timeoutSource.Token);
            var heldResult = await heldInvocation.Completion.WaitAsync(timeoutSource.Token);
            heldResult.IsError.Should().NotBeTrue();

            var retryQuery = await target.CallToolAsync(
                "host-valid-query",
                new Dictionary<string, object?>
                {
                    ["workspace"] = firstSelector,
                    ["name"] = "Retry",
                },
                timeoutSource.Token);
            retryQuery.IsError.Should().NotBeTrue();

            var retryTransaction = await target.CallToolAsync(
                "transaction-start",
                new Dictionary<string, object?>
                {
                    ["workspace"] = firstSelector,
                },
                timeoutSource.Token);
            retryTransaction.IsError.Should().NotBeTrue();

            var rollback = await target.CallToolAsync(
                "transaction-rollback",
                new Dictionary<string, object?>
                {
                    ["workspace"] = firstSelector,
                },
                timeoutSource.Token);
            rollback.IsError.Should().NotBeTrue();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_ThrowingExternalQuery_WHEN_InvokingTool_THEN_ShouldSanitiseFailureAndKeepHostUsable()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets: [AcceptancePluginAsset.HostQuery]);

        try
        {
            var workspace = await OpenWorkspaceAsync(
                target,
                Path.Combine(target.WorkspaceRoot, "Sample.csproj"),
                target.WorkspaceRoot);
            var workspaceSelector = workspace.CreateSelector();

            var throwingResult = await target.CallToolAsync(
                "host-valid-query",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["name"] = "Throw",
                    ["throw"] = true,
                },
                TestContext.Current.CancellationToken);

            throwingResult.IsError.Should().BeTrue();
            var error = AcceptanceProtocol.GetError(throwingResult);
            error.GetProperty("code").GetString().Should().Be("UnhandledException");
            error.GetProperty("message").GetString().Should().Be("Tool execution failed.");
            error.GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
            throwingResult.StructuredContent!.Value.GetRawText().Should().NotContain("Sensitive query failure");
            throwingResult.StructuredContent.Value.GetRawText().Should().NotContain(nameof(InvalidOperationException));

            var successfulQuery = await target.CallToolAsync(
                "host-valid-query",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["name"] = "Recovered",
                },
                TestContext.Current.CancellationToken);
            successfulQuery.IsError.Should().NotBeTrue();

            var statusResult = await target.CallToolAsync(
                "server-status",
                new Dictionary<string, object?>(),
                TestContext.Current.CancellationToken);
            statusResult.IsError.Should().NotBeTrue();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_SolutionProjectOutsideWorkspaceRoot_WHEN_Opening_THEN_ShouldRejectPublicPathBoundary()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var outsideRoot = target.CopyWorkspaceAsset(AcceptanceWorkspaceAsset.SdkProject, "outside-root");
            var outsideProjectPath = Path.Combine(outsideRoot, "Sample.csproj");
            var relativeProjectPath = Path.GetRelativePath(target.WorkspaceRoot, outsideProjectPath)
                .Replace(Path.DirectorySeparatorChar, '/');
            var solutionPath = Path.Combine(target.WorkspaceRoot, "OutsideRoot.slnx");
            var solutionText = $"<Solution>\r\n  <Project Path=\"{relativeProjectPath}\" />\r\n</Solution>\r\n";
            await File.WriteAllTextAsync(solutionPath, solutionText, TestContext.Current.CancellationToken);

            var openResult = await target.CallToolAsync(
                "workspace-open",
                new Dictionary<string, object?>
                {
                    ["path"] = solutionPath,
                    ["workspaceRoot"] = target.WorkspaceRoot,
                },
                TestContext.Current.CancellationToken);

            openResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(openResult)
                .GetProperty("code")
                .GetString()
                .Should()
                .Be("WorkspaceProjectOutsideRoot");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<AcceptanceWorkspaceIdentity> OpenWorkspaceAsync(
        AcceptanceProcessFixture target,
        string path,
        string workspaceRoot,
        string? alias = null)
    {
        var result = await target.CallToolAsync(
            "workspace-open",
            new Dictionary<string, object?>
            {
                ["alias"] = alias,
                ["path"] = path,
                ["workspaceRoot"] = workspaceRoot,
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().NotBeTrue();
        return AcceptanceWorkspaceIdentity.FromOpenResult(result);
    }

    private static Dictionary<string, object?> CreateControlledQueryArguments(
        IReadOnlyDictionary<string, object?> workspaceSelector,
        string name,
        string controlDirectory)
    {
        return new Dictionary<string, object?>
        {
            ["workspace"] = workspaceSelector,
            ["name"] = name,
            ["controlDirectory"] = controlDirectory,
        };
    }

    private static void AssertWorkspaceBusy(ModelContextProtocol.Protocol.CallToolResult result)
    {
        result.IsError.Should().BeTrue();
        AcceptanceProtocol.GetError(result).GetProperty("code").GetString().Should().Be("WorkspaceBusy");
        result.StructuredContent!.Value.GetProperty("next").GetString().Should().Be("Retry");
    }

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> StartTransactionWhenAvailableAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await target.CallToolAsync(
                "transaction-start",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                cancellationToken);

            if (result.IsError != true
                || AcceptanceProtocol.GetError(result).GetProperty("code").GetString() != "WorkspaceBusy")
            {
                return result;
            }

            await Task.Yield();
        }
    }
}
