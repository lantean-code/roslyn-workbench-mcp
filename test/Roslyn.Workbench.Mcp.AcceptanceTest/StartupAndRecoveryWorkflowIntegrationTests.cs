using System.Text.Json;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class StartupAndRecoveryWorkflowIntegrationTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GIVEN_InvalidConfigurationAndBlockedRecovery_WHEN_RestartingPublishedHost_THEN_ShouldReportFallbackAndDurableRecoveryState()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: ["--default-max-results=invalid"]);

        try
        {
            var initialStatusResult = await target.CallToolAsync(
                "server-status",
                new Dictionary<string, object?>
                {
                    ["detail"] = "Full",
                },
                TestContext.Current.CancellationToken);

            var initialStatus = AcceptanceProtocol.GetSuccessData(initialStatusResult);

            initialStatusResult.IsError.Should().NotBeTrue();
            initialStatus.GetProperty("configuration").GetProperty("defaultMaxResults").GetInt32().Should().Be(100);
            initialStatus.GetProperty("startupWarnings")
                .EnumerateArray()
                .Should()
                .ContainSingle(warning =>
                    warning.GetProperty("code").GetString() == "StartupConfigurationFallback"
                    && (warning.GetProperty("message").GetString() ?? string.Empty).Contains("--default-max-results", StringComparison.Ordinal));

            var commitId = "acceptance-recovery-conflict";
            var manifestDirectory = Path.Combine(target.StateRoot, "recovery", commitId);
            var manifestPath = Path.Combine(manifestDirectory, "manifest.json");
            var loadedPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            AcceptanceStateDirectory.Create(manifestDirectory);

            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(
                    new
                    {
                        version = 1,
                        commitId,
                        loadedPath,
                        workspaceRoot = target.WorkspaceRoot,
                        state = "RecoveryConflict",
                        entries = Array.Empty<object>(),
                        createdDirectories = Array.Empty<string>(),
                        message = "Acceptance recovery conflict.",
                    },
                    _serializerOptions),
                TestContext.Current.CancellationToken);

            AcceptanceStateDirectory.MakeFilePrivate(manifestPath);

            await target.RestartAsync(TestContext.Current.CancellationToken);

            var restartedStatusResult = await target.CallToolAsync(
                "server-status",
                new Dictionary<string, object?>
                {
                    ["detail"] = "Full",
                },
                TestContext.Current.CancellationToken);

            var restartedStatus = AcceptanceProtocol.GetSuccessData(restartedStatusResult);

            restartedStatusResult.IsError.Should().NotBeTrue();
            restartedStatus.GetProperty("recovery")
                .EnumerateArray()
                .Should()
                .ContainSingle(recovery =>
                    recovery.GetProperty("commitId").GetString() == commitId
                    && recovery.GetProperty("solutionPath").GetString() == loadedPath
                    && recovery.GetProperty("state").GetString() == "RecoveryConflict"
                    && recovery.GetProperty("message").GetString() == "Acceptance recovery conflict.");

            var blockedOpenResult = await target.CallToolAsync(
                "workspace-open",
                new Dictionary<string, object?>
                {
                    ["path"] = loadedPath,
                    ["workspaceRoot"] = target.WorkspaceRoot,
                },
                TestContext.Current.CancellationToken);

            blockedOpenResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(blockedOpenResult).GetProperty("code").GetString().Should().Be("RecoveryPending");
            var continuation = AcceptanceProtocol.GetContinuation(blockedOpenResult);
            continuation.GetProperty("kind").GetString().Should().Be("ResolveExternally");
            continuation.GetProperty("instruction").GetString().Should().NotBeNullOrWhiteSpace();
            File.Exists(manifestPath).Should().BeTrue();

            using var persistedManifest = JsonDocument.Parse(
                await File.ReadAllTextAsync(manifestPath, TestContext.Current.CancellationToken));

            persistedManifest.RootElement.GetProperty("state").GetString().Should().Be("RecoveryConflict");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }
}
