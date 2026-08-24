using System.Text.Json;

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
            var correlationId = error.GetProperty("correlationId").GetString();
            correlationId.Should().NotBeNullOrWhiteSpace();
            var structuredContent = throwingResult.StructuredContent;
            structuredContent.Should().NotBeNull();
            AcceptanceProtocol.AssertTextContentMatchesStructuredContent(throwingResult);
            var structuredError = structuredContent.GetValueOrDefault();
            structuredError
                .GetProperty("diagnostics")
                .GetProperty("detailsTool")
                .GetString()
                .Should()
                .Be("get-error-details");
            structuredError
                .GetProperty("reporting")
                .GetProperty("canPrepare")
                .GetBoolean()
                .Should()
                .BeFalse();
            structuredError.GetRawText().Should().NotContain("Sensitive query failure");
            structuredError.GetRawText().Should().NotContain(nameof(InvalidOperationException));

            var detailsResult = await target.CallToolAsync(
                "get-error-details",
                new Dictionary<string, object?>
                {
                    ["correlationId"] = correlationId,
                },
                TestContext.Current.CancellationToken);
            detailsResult.IsError.Should().NotBeTrue();
            var details = AcceptanceProtocol.GetSuccessData(detailsResult);
            details.GetProperty("sensitivity").GetString().Should().Be("LocalDiagnostic");
            details.GetProperty("safeForExternalSubmission").GetBoolean().Should().BeFalse();
            details.GetProperty("error").GetProperty("correlationId").GetString().Should().Be(correlationId);
            details.GetRawText().Should().Contain("Sensitive query failure");

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
    public async Task GIVEN_ConfiguredPromptReportingWithoutClientElicitation_WHEN_PreparingAndSubmitting_THEN_ShouldReviewLocallyAndFailClosed()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets: [AcceptancePluginAsset.HostQuery]);

        try
        {
            var toolNames = (await target.ListToolsAsync(TestContext.Current.CancellationToken))
                .Select(static tool => tool.Name)
                .ToArray();
            toolNames.Should().Contain(["get-error-details", "prepare-error-report", "submit-error-report"]);

            var workspace = await OpenWorkspaceAsync(
                target,
                Path.Combine(target.WorkspaceRoot, "Sample.csproj"),
                target.WorkspaceRoot);
            var throwingResult = await target.CallToolAsync(
                "host-valid-query",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspace.CreateSelector(),
                    ["name"] = "Throw",
                    ["throw"] = true,
                },
                TestContext.Current.CancellationToken);

            var correlationId = AcceptanceProtocol.GetError(throwingResult)
                .GetProperty("correlationId")
                .GetString();
            var prepareResult = await target.CallToolAsync(
                "prepare-error-report",
                new Dictionary<string, object?>
                {
                    ["correlationId"] = correlationId,
                },
                TestContext.Current.CancellationToken);
            prepareResult.IsError.Should().NotBeTrue();
            var prepared = AcceptanceProtocol.GetSuccessData(prepareResult);
            prepared.GetProperty("dispatcher").GetString().Should().Be("Logging");
            prepared.GetProperty("destination").GetString().Should().Be("standard error (stderr)");
            var payloadJson = prepared.GetProperty("payloadJson").GetString()
                ?? throw new InvalidOperationException("The prepared logging payload must include its JSON representation.");
            payloadJson.Should().NotContain("Sensitive query failure");
            payloadJson.Should().NotContain(target.WorkspaceRoot);

            var submitResult = await target.CallToolAsync(
                "submit-error-report",
                new Dictionary<string, object?>
                {
                    ["submissionHandle"] = prepared.GetProperty("submissionHandle").GetString(),
                },
                TestContext.Current.CancellationToken);
            submitResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(submitResult)
                .GetProperty("code")
                .GetString()
                .Should()
                .Be("ApprovalUnavailable");

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
    public async Task GIVEN_LoggingFallbackAndAlwaysConsent_WHEN_SubmittingPreparedReport_THEN_ShouldWriteSanitisedReportToStandardError()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: ["--error-reporting-consent", "always"],
            pluginAssets: [AcceptancePluginAsset.HostQuery]);

        try
        {
            var workspace = await OpenWorkspaceAsync(
                target,
                Path.Combine(target.WorkspaceRoot, "Sample.csproj"),
                target.WorkspaceRoot);
            var throwingResult = await target.CallToolAsync(
                "host-valid-query",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspace.CreateSelector(),
                    ["name"] = "Throw",
                    ["throw"] = true,
                },
                TestContext.Current.CancellationToken);

            var correlationId = AcceptanceProtocol.GetError(throwingResult)
                .GetProperty("correlationId")
                .GetString();
            var prepareResult = await target.CallToolAsync(
                "prepare-error-report",
                new Dictionary<string, object?>
                {
                    ["correlationId"] = correlationId,
                },
                TestContext.Current.CancellationToken);
            prepareResult.IsError.Should().NotBeTrue();
            var prepared = AcceptanceProtocol.GetSuccessData(prepareResult);
            prepared.GetProperty("dispatcher").GetString().Should().Be("Logging");
            prepared.GetProperty("destination").GetString().Should().Be("standard error (stderr)");
            var payloadJson = prepared.GetProperty("payloadJson").GetString()
                ?? throw new InvalidOperationException("The prepared logging payload must include its JSON representation.");
            using var payloadDocument = JsonDocument.Parse(payloadJson);
            var payload = payloadDocument.RootElement;
            var reportId = payload.GetProperty("report").GetProperty("reportId").GetString()
                ?? throw new InvalidOperationException("The prepared logging payload must include its report identifier.");

            var submitResult = await target.CallToolAsync(
                "submit-error-report",
                new Dictionary<string, object?>
                {
                    ["submissionHandle"] = prepared.GetProperty("submissionHandle").GetString(),
                },
                TestContext.Current.CancellationToken);
            submitResult.IsError.Should().NotBeTrue();
            var submitted = AcceptanceProtocol.GetSuccessData(submitResult);
            submitted.GetProperty("dispatcher").GetString().Should().Be("Logging");
            submitted.GetProperty("reportReference").GetString().Should().Be(reportId);

            var statusResult = await target.CallToolAsync(
                "server-status",
                new Dictionary<string, object?>(),
                TestContext.Current.CancellationToken);
            statusResult.IsError.Should().NotBeTrue();

            var standardError = await target.WaitForStandardErrorAsync(
                "User-approved error report",
                TestContext.Current.CancellationToken);

            await target.StopAsync();
            standardError.Should().Contain("User-approved error report");
            var approvedReportOffset = standardError.IndexOf("User-approved error report", StringComparison.Ordinal);
            var approvedReportLog = standardError[approvedReportOffset..];
            approvedReportLog.Should().Contain(reportId);
            approvedReportLog.Should().Contain("external-plugin-tool");
            approvedReportLog.Should().NotContain("Sensitive query failure");
            approvedReportLog.Should().NotContain(target.WorkspaceRoot);
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
        var continuation = AcceptanceProtocol.GetContinuation(result);
        continuation.GetProperty("kind").GetString().Should().Be("RetryRequest");
        continuation.GetProperty("instruction").GetString().Should().NotBeNullOrWhiteSpace();
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
