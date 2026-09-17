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
        toolNames.Should().NotContain("transaction-validate");
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
        configuration.GetProperty("compilerValidationRequired").GetBoolean().Should().BeFalse();
        configuration.GetProperty("externalPluginsEnabled").GetBoolean().Should().BeFalse();
        configuration.GetProperty("clientSupportsElicitation").GetBoolean().Should().BeFalse();

        var invocation = async () => await target.CallToolAsync(
            "transaction-start",
            new Dictionary<string, object?>(),
            TestContext.Current.CancellationToken);

        await invocation.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*transaction-start*");
    }

    [Theory]
    [InlineData("transactional")]
    [InlineData("approval-required")]
    [InlineData("autonomous-trusted")]
    public async Task GIVEN_MutationModeWithCompilerValidation_WHEN_ListingTools_THEN_ShouldPublishValidationPolicy(
        string operationalMode)
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: ["--commit-validation", "no-new-compiler-errors"],
            operationalMode: operationalMode);

        var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
        tools.Select(static tool => tool.Name).Should().Contain("transaction-validate");

        var status = await target.CallToolAsync(
            "server-status",
            new Dictionary<string, object?> { ["detail"] = "Full" },
            TestContext.Current.CancellationToken);

        var configuration = AcceptanceProtocol.GetSuccessData(status).GetProperty("configuration");
        configuration.GetProperty("operationalMode").GetString().Should().Be(operationalMode);
        configuration.GetProperty("compilerValidationRequired").GetBoolean().Should().BeTrue();
        target.ServerInstructions.Should().Contain("transaction-validate");
    }

    [Fact]
    public async Task GIVEN_InspectionOnlyWithCompilerValidation_WHEN_StartingHost_THEN_ShouldFailInitialisation()
    {
        var action = async () => await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: ["--commit-validation", "no-new-compiler-errors"],
            operationalMode: "inspection-only");

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("MCP initialization failed");
        exception.Which.Message.Should().Contain("--commit-validation");
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
    public async Task GIVEN_ApprovalRequiredMode_WHEN_ReviewingAndApprovingReceipt_THEN_ShouldPublishExactChangeWorkflowAndRejectReplay()
    {
        ValueTask<ElicitResult> HandleElicitationAsync(
            ElicitRequestParams? request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AssertReceiptApproval(request);
            return ValueTask.FromResult(CreateElicitationResult("approve-this-receipt"));
        }

        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            elicitationHandler: HandleElicitationAsync,
            operationalMode: "approval-required");

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            var toolNames = tools.Select(static tool => tool.Name).ToArray();
            toolNames.Should().Contain("transaction-review");
            toolNames.Should().Contain("transaction-commit");
            toolNames.Should().NotContain("transaction-preview");

            var previewInvocation = async () => await target.CallToolAsync(
                "transaction-preview",
                new Dictionary<string, object?>(),
                TestContext.Current.CancellationToken);

            await previewInvocation.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*transaction-preview*");

            target.ServerInstructions.Should().Contain("call transaction-review for an exact-change receipt");
            target.ServerInstructions.Should().Contain("transaction-preview is not available");

            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"), "sample");
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);
            var mutation = await RenameAsync(target, workspaceSelector, workspace, "ReceiptApprovedClass");
            var mutationSnapshot = AcceptanceProtocol.GetSnapshot(mutation);
            var review = await ReviewAsync(target, workspaceSelector, mutationSnapshot);
            var reviewData = AcceptanceProtocol.GetSuccessData(review);
            var receiptId = reviewData.GetProperty("receiptId").GetString()
                ?? throw new InvalidOperationException("The transaction review did not return a receipt identifier.");

            receiptId.Should().NotBeNullOrWhiteSpace();
            reviewData.GetProperty("changeSetDigest").GetString().Should().HaveLength(64);
            reviewData.GetProperty("modifiedDocumentCount").GetInt32().Should().Be(1);

            var commit = await CommitReceiptAsync(target, workspaceSelector, AcceptanceProtocol.GetSnapshot(review), receiptId);

            commit.IsError.Should().NotBeTrue();
            (await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken)).Should().Contain("class ReceiptApprovedClass");

            var replay = await CommitReceiptAsync(target, workspaceSelector, mutationSnapshot, receiptId);
            replay.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(replay).GetProperty("code").GetString().Should().Be("TransactionReceiptUnavailable");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Theory]
    [InlineData(false, "ApprovalUnavailable")]
    [InlineData(true, "TransactionCommitNotApproved")]
    public async Task GIVEN_ApprovalRequiredCommitCannotBeApproved_WHEN_Committing_THEN_ShouldRetainFilesTransactionAndReceipt(
        bool clientCanElicit,
        string expectedCode)
    {
        ValueTask<ElicitResult> RefuseAsync(
            ElicitRequestParams? request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AssertReceiptApproval(request);
            return ValueTask.FromResult(CreateElicitationResult("do-not-commit"));
        }

        Func<ElicitRequestParams?, CancellationToken, ValueTask<ElicitResult>>? handler = null;
        if (clientCanElicit)
        {
            handler = RefuseAsync;
        }

        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            elicitationHandler: handler,
            operationalMode: "approval-required");

        try
        {
            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            var originalBytes = await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken);
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"), "sample");
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);
            var mutation = await RenameAsync(target, workspaceSelector, workspace, "UnapprovedClass");
            var review = await ReviewAsync(target, workspaceSelector, AcceptanceProtocol.GetSnapshot(mutation));
            var firstReceiptId = AcceptanceProtocol.GetSuccessData(review).GetProperty("receiptId").GetString()
                ?? throw new InvalidOperationException("The transaction review did not return a receipt identifier.");

            var commit = await CommitReceiptAsync(
                target,
                workspaceSelector,
                AcceptanceProtocol.GetSnapshot(review),
                firstReceiptId);

            commit.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(commit).GetProperty("code").GetString().Should().Be(expectedCode);
            (await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken)).Should().Equal(originalBytes);

            var repeatedReview = await ReviewAsync(target, workspaceSelector, AcceptanceProtocol.GetSnapshot(review));
            var repeatedReceiptId = AcceptanceProtocol.GetSuccessData(repeatedReview).GetProperty("receiptId").GetString();
            repeatedReceiptId.Should().Be(firstReceiptId);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static Task<CallToolResult> ReviewAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        IReadOnlyDictionary<string, object?> expectedSnapshot)
    {
        return target.CallToolAsync(
            "transaction-review",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["expectedSnapshot"] = expectedSnapshot,
            },
            TestContext.Current.CancellationToken);
    }

    private static Task<CallToolResult> CommitReceiptAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        IReadOnlyDictionary<string, object?> expectedSnapshot,
        string receiptId)
    {
        return target.CallToolAsync(
            "transaction-commit",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["expectedSnapshot"] = expectedSnapshot,
                ["receiptId"] = receiptId,
            },
            TestContext.Current.CancellationToken);
    }

    private static void AssertReceiptApproval(ElicitRequestParams? elicitation)
    {
        var request = elicitation
            ?? throw new InvalidOperationException("The receipt approval elicitation was not supplied.");

        request.Message.Should().Contain("Commit reviewed change");
        request.Message.Should().Contain("affecting 1 file(s)");

        var schema = request.RequestedSchema
            ?? throw new InvalidOperationException("The receipt approval did not include a form schema.");

        var choiceSchema = schema.Properties["choice"]
            .Should()
            .BeOfType<ElicitRequestParams.TitledSingleSelectEnumSchema>()
            .Which;

        choiceSchema.OneOf.Select(static option => option.Const).Should().Equal(
            "approve-this-receipt",
            "do-not-commit");
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
