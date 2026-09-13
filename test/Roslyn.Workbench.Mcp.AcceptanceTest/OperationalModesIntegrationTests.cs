using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class OperationalModesIntegrationTests
{
    [Fact]
    public async Task GIVEN_NoOperationalMode_WHEN_ListingTools_THEN_ShouldPublishInspectionOnlyCatalogue()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            operationalMode: null);

        var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
        var toolNames = tools.Select(static tool => tool.Name).ToArray();

        toolNames.Should().Contain("list-code-actions");
        toolNames.Should().Contain("prepare-fix-all");
        toolNames.Should().NotContain("transaction-start");
        toolNames.Should().NotContain("transaction-preview");
        toolNames.Should().NotContain("transaction-history");
        toolNames.Should().NotContain("transaction-commit");
        toolNames.Should().NotContain("transaction-rollback");
        toolNames.Should().NotContain("format-document");
        toolNames.Should().NotContain("rename-symbol");
        toolNames.Should().NotContain("stage-code-action");

        var status = await target.CallToolAsync(
            "server-status",
            new Dictionary<string, object?>
            {
                ["detail"] = "Full",
            },
            TestContext.Current.CancellationToken);

        var configuration = AcceptanceProtocol.GetSuccessData(status).GetProperty("configuration");

        configuration.GetProperty("operationalMode").GetString().Should().Be("inspection-only");
        configuration.GetProperty("sourceMutationEnabled").GetBoolean().Should().BeFalse();
        configuration.GetProperty("externalPluginsEnabled").GetBoolean().Should().BeFalse();
        configuration.GetProperty("clientSupportsElicitation").GetBoolean().Should().BeFalse();

        var invocation = async () => await target.CallToolAsync(
            "transaction-start",
            new Dictionary<string, object?>(),
            TestContext.Current.CancellationToken);

        await invocation.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*transaction-start*");
    }

    [Fact]
    public async Task GIVEN_InspectionOnlyModeAndEnabledExternalPlugins_WHEN_ListingTools_THEN_ShouldPublishOnlyPluginQueries()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets:
            [
                AcceptancePluginAsset.HostQuery,
                AcceptancePluginAsset.HostMutation,
            ],
            operationalMode: "inspection-only");

        var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
        var toolNames = tools.Select(static tool => tool.Name).ToArray();

        toolNames.Should().Contain("host-valid-query");
        toolNames.Should().NotContain("host-valid-mutation");

        var status = await target.CallToolAsync(
            "server-status",
            new Dictionary<string, object?> { ["detail"] = "Full" },
            TestContext.Current.CancellationToken);

        var configuration = AcceptanceProtocol.GetSuccessData(status).GetProperty("configuration");

        configuration.GetProperty("operationalMode").GetString().Should().Be("inspection-only");
        configuration.GetProperty("externalPluginsEnabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_PluginDirectoryWithoutEnableSwitch_WHEN_StartingHost_THEN_ShouldFailInitialisation()
    {
        var action = async () => await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets: [AcceptancePluginAsset.HostQuery],
            operationalMode: "inspection-only",
            enablePlugins: false);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("MCP initialization failed");
        exception.Which.Message.Should().Contain("--enable-plugins");
    }

    [Fact]
    public async Task GIVEN_ApprovalRequiredMode_WHEN_StartingHost_THEN_ShouldFailUntilReceiptApprovalIsImplemented()
    {
        var action = async () => await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            operationalMode: "approval-required");

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("MCP initialization failed");
        exception.Which.Message.Should().Contain(
            "approval-required operational mode is unavailable until receipt-bound approval is implemented");
    }

    [Fact]
    public async Task GIVEN_AutonomousTrustedMode_WHEN_Committing_THEN_ShouldPublishMutationToolsAndPersistWithoutElicitation()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            operationalMode: "autonomous-trusted");

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            var toolNames = tools.Select(static tool => tool.Name).ToArray();

            toolNames.Should().Contain("transaction-start");
            toolNames.Should().Contain("transaction-commit");
            toolNames.Should().Contain("rename-symbol");
            toolNames.Should().Contain("stage-code-action");

            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            var workspace = await OpenWorkspaceAsync(
                target,
                Path.Combine(target.WorkspaceRoot, "Sample.csproj"),
                "sample");

            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);
            var mutation = await RenameAsync(target, workspaceSelector, workspace, "AutonomousClass");
            var commit = await CommitAsync(target, workspaceSelector, AcceptanceProtocol.GetSnapshot(mutation));

            commit.IsError.Should().NotBeTrue();
            var documentText = await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken);
            documentText.Should().Contain("class AutonomousClass");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_TransactionalModeAndExplicitRefusal_WHEN_Committing_THEN_ShouldLeaveFilesAndTransactionUnchanged()
    {
        ValueTask<ElicitResult> HandleElicitationAsync(
            ElicitRequestParams? request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AssertCommitConfirmation(request);
            return ValueTask.FromResult(CreateElicitationResult("do-not-commit"));
        }

        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            elicitationHandler: HandleElicitationAsync,
            operationalMode: "transactional");

        try
        {
            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            var originalBytes = await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken);
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"), "sample");
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);
            var mutation = await RenameAsync(target, workspaceSelector, workspace, "Class2");

            var commit = await CommitAsync(target, workspaceSelector, AcceptanceProtocol.GetSnapshot(mutation));

            commit.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(commit).GetProperty("code").GetString().Should().Be("TransactionCommitNotApproved");
            (await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken)).Should().Equal(originalBytes);

            var preview = await target.CallToolAsync(
                "transaction-preview",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            preview.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(preview).GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(1);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_TransactionalModeWithoutElicitation_WHEN_Committing_THEN_ShouldPreserveTheActiveTransaction()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            operationalMode: "transactional");

        try
        {
            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            var originalBytes = await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken);
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"), "sample");
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);
            var mutation = await RenameAsync(target, workspaceSelector, workspace, "Class2");

            var commit = await CommitAsync(target, workspaceSelector, AcceptanceProtocol.GetSnapshot(mutation));

            commit.IsError.Should().BeTrue();
            var error = AcceptanceProtocol.GetError(commit);
            error.GetProperty("code").GetString().Should().Be("ApprovalUnavailable");
            error.GetProperty("message").GetString().Should().Contain("Enable interactive MCP requests");
            (await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken)).Should().Equal(originalBytes);

            var preview = await target.CallToolAsync(
                "transaction-preview",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            preview.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(preview).GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(1);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_TransactionalSessionApproval_WHEN_CommittingAcrossWorkspacesAndRestarting_THEN_ShouldRememberOnlyForTheProcessLifetime()
    {
        var elicitationCount = 0;

        ValueTask<ElicitResult> HandleElicitationAsync(
            ElicitRequestParams? request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AssertCommitConfirmation(request);
            elicitationCount++;
            var choice = elicitationCount == 1 ? "commit-for-session" : "commit-once";
            return ValueTask.FromResult(CreateElicitationResult(choice));
        }

        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            elicitationHandler: HandleElicitationAsync,
            operationalMode: "transactional");

        try
        {
            var secondRoot = target.CopyWorkspaceAsset(AcceptanceWorkspaceAsset.SdkProject, "second");
            var afterRestartRoot = target.CopyWorkspaceAsset(AcceptanceWorkspaceAsset.SdkProject, "after-restart");
            var first = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"), "first");
            var second = await OpenWorkspaceAsync(target, Path.Combine(secondRoot, "Sample.csproj"), "second", secondRoot);

            await RenameAndCommitAsync(target, first, "FirstRenamedClass");
            await RenameAndCommitAsync(target, second, "SecondRenamedClass");

            elicitationCount.Should().Be(1);
            await target.RestartAsync(TestContext.Current.CancellationToken);

            var afterRestart = await OpenWorkspaceAsync(
                target,
                Path.Combine(afterRestartRoot, "Sample.csproj"),
                "after-restart",
                afterRestartRoot);

            await RenameAndCommitAsync(target, afterRestart, "AfterRestartRenamedClass");

            elicitationCount.Should().Be(2);
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
        string alias,
        string? workspaceRoot = null)
    {
        var result = await target.CallToolAsync(
            "workspace-open",
            new Dictionary<string, object?>
            {
                ["alias"] = alias,
                ["path"] = path,
                ["workspaceRoot"] = workspaceRoot ?? target.WorkspaceRoot,
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().NotBeTrue();
        return AcceptanceWorkspaceIdentity.FromOpenResult(result);
    }

    private static async Task RenameAndCommitAsync(
        AcceptanceProcessFixture target,
        AcceptanceWorkspaceIdentity workspace,
        string newName)
    {
        var workspaceSelector = workspace.CreateSelector();
        await StartTransactionAsync(target, workspaceSelector);
        var mutation = await RenameAsync(target, workspaceSelector, workspace, newName);
        var commit = await CommitAsync(target, workspaceSelector, AcceptanceProtocol.GetSnapshot(mutation));

        commit.IsError.Should().NotBeTrue();
    }

    private static async Task StartTransactionAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector)
    {
        var result = await target.CallToolAsync(
            "transaction-start",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().NotBeTrue();
    }

    private static Task<CallToolResult> RenameAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        AcceptanceWorkspaceIdentity workspace,
        string newName)
    {
        return target.CallToolAsync(
            "rename-symbol",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["symbol"] = new Dictionary<string, object?>
                {
                    ["documentationCommentId"] = "T:Sample.Class1",
                },
                ["newName"] = newName,
                ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
            },
            TestContext.Current.CancellationToken);
    }

    private static Task<CallToolResult> CommitAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        IReadOnlyDictionary<string, object?> expectedSnapshot)
    {
        return target.CallToolAsync(
            "transaction-commit",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["expectedSnapshot"] = expectedSnapshot,
            },
            TestContext.Current.CancellationToken);
    }

    private static void AssertCommitConfirmation(ElicitRequestParams? request)
    {
        var confirmation = request
            ?? throw new InvalidOperationException("The transaction confirmation was not supplied.");

        confirmation.Message.Should().Be("Commit the current staged transaction to source files?");
        var schema = confirmation.RequestedSchema
            ?? throw new InvalidOperationException("The transaction confirmation did not include a form schema.");

        schema.Required.Should().Equal("choice");
        var choiceSchema = schema.Properties["choice"]
            .Should()
            .BeOfType<ElicitRequestParams.TitledSingleSelectEnumSchema>()
            .Which;

        choiceSchema.OneOf.Select(static option => option.Const).Should().Equal(
            "commit-once",
            "commit-for-session",
            "do-not-commit");

        choiceSchema.OneOf.Select(static option => option.Title).Should().Equal(
            "Yes, commit once",
            "Yes, for this session",
            "No, don't commit");
    }

    private static ElicitResult CreateElicitationResult(string choice)
    {
        return new ElicitResult
        {
            Action = "accept",
            Content = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["choice"] = JsonSerializer.SerializeToElement(choice),
            },
        };
    }
}
