namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class WorkspaceSelectionIntegrationTests
{
    [Fact]
    public async Task GIVEN_OneWorkspace_WHEN_SelectingByIdAliasPathOrImplicitly_THEN_ShouldResolveTheSameWorkspace()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var openResult = await OpenWorkspaceAsync(
                target,
                Path.Combine(target.WorkspaceRoot, "Sample.csproj"),
                "selection");
            var identity = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
            var selectors = new[]
            {
                identity.CreateSelector(),
                new Dictionary<string, object?> { ["alias"] = identity.Alias },
                new Dictionary<string, object?> { ["path"] = identity.LoadedPath },
            };

            foreach (var selector in selectors)
            {
                var status = await GetStatusAsync(target, selector);
                AcceptanceProtocol.GetSuccessData(status)
                    .GetProperty("workspace")
                    .GetProperty("workspaceId")
                    .GetGuid()
                    .Should()
                    .Be(identity.WorkspaceId);
            }

            var implicitStatus = await GetStatusAsync(target, workspaceSelector: null);
            AcceptanceProtocol.GetSuccessData(implicitStatus)
                .GetProperty("workspace")
                .GetProperty("workspaceId")
                .GetGuid()
                .Should()
                .Be(identity.WorkspaceId);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_DuplicateAndTransactionOwnedWorkspace_WHEN_OpeningOrClosing_THEN_ShouldRejectUnsafeLifecycleChanges()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var openResult = await OpenWorkspaceAsync(target, projectPath, "lifecycle");
            var workspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult).CreateSelector();

            var duplicateResult = await OpenWorkspaceAsync(target, projectPath, "duplicate");
            duplicateResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(duplicateResult).GetProperty("code").GetString().Should().Be("WorkspaceAlreadyOpen");

            await target.CallToolAsync(
                "transaction-start",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspace,
                },
                TestContext.Current.CancellationToken);

            var closeResult = await target.CallToolAsync(
                "workspace-close",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspace,
                },
                TestContext.Current.CancellationToken);

            closeResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(closeResult).GetProperty("code").GetString().Should().Be("TransactionOpen");
            var continuation = AcceptanceProtocol.GetContinuation(closeResult);
            continuation.GetProperty("kind").GetString().Should().Be("ChooseTool");
            continuation.GetProperty("tools").EnumerateArray().Select(static item => item.GetString()).Should().Equal(
                "transaction-commit",
                "transaction-rollback");
            continuation.GetProperty("instruction").GetString().Should().NotBeNullOrWhiteSpace();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_MultipleWorkspaces_WHEN_OmittingSelectorOrClosingOne_THEN_ShouldRequireSelectionAndKeepTheOtherUsable()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var secondWorkspaceRoot = target.CopyWorkspaceAsset(AcceptanceWorkspaceAsset.SdkProject, "second");
            var firstOpen = await OpenWorkspaceAsync(
                target,
                Path.Combine(target.WorkspaceRoot, "Sample.csproj"),
                "first");
            var secondOpen = await target.CallToolAsync(
                "workspace-open",
                new Dictionary<string, object?>
                {
                    ["alias"] = "second",
                    ["path"] = Path.Combine(secondWorkspaceRoot, "Sample.csproj"),
                    ["workspaceRoot"] = secondWorkspaceRoot,
                },
                TestContext.Current.CancellationToken);

            var ambiguousStatus = await GetStatusAsync(target, workspaceSelector: null);
            ambiguousStatus.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(ambiguousStatus).GetProperty("code").GetString().Should().Be("WorkspaceSelectorRequired");

            var firstSelector = AcceptanceWorkspaceIdentity.FromOpenResult(firstOpen).CreateSelector();
            var secondSelector = AcceptanceWorkspaceIdentity.FromOpenResult(secondOpen).CreateSelector();
            await target.CallToolAsync(
                "workspace-close",
                new Dictionary<string, object?>
                {
                    ["workspace"] = firstSelector,
                },
                TestContext.Current.CancellationToken);

            var searchResult = await target.CallToolAsync(
                "search-symbols",
                new Dictionary<string, object?>
                {
                    ["workspace"] = secondSelector,
                    ["query"] = "Class1",
                },
                TestContext.Current.CancellationToken);

            searchResult.IsError.Should().NotBeTrue();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> OpenWorkspaceAsync(
        AcceptanceProcessFixture target,
        string path,
        string alias)
    {
        return await target.CallToolAsync(
            "workspace-open",
            new Dictionary<string, object?>
            {
                ["alias"] = alias,
                ["path"] = path,
                ["workspaceRoot"] = target.WorkspaceRoot,
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> GetStatusAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?>? workspaceSelector)
    {
        var arguments = new Dictionary<string, object?>();
        if (workspaceSelector is not null)
        {
            arguments["workspace"] = workspaceSelector;
        }

        return await target.CallToolAsync(
            "workspace-status",
            arguments,
            TestContext.Current.CancellationToken);
    }
}
