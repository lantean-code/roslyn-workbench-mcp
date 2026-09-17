using System.Diagnostics;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class MutationTransactionIntegrationTests
{
    private const long _compilerValidationPerformanceBudgetMilliseconds = 10_000;

    [Fact]
    public async Task GIVEN_IntroducedCompilerError_WHEN_CorrectingAndRetryingPublishedCommit_THEN_ShouldPersistOnlyCorrectedRevision()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: ["--commit-validation", "no-new-compiler-errors"],
            pluginAssets: [AcceptancePluginAsset.HostMutation]);

        try
        {
            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            var originalBytes = await File.ReadAllBytesAsync(
                documentPath,
                TestContext.Current.CancellationToken);

            var workspace = await OpenWorkspaceAsync(
                target,
                Path.Combine(target.WorkspaceRoot, "Sample.csproj"));

            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);
            var invalidMutation = await StageExternalMutationAsync(
                target,
                workspaceSelector,
                workspace.CreateSnapshot(transactionRevision: 0),
                "Class1.cs",
                "Class1",
                "Class1 : MissingBase");

            var invalidSnapshot = AcceptanceProtocol.GetSnapshot(invalidMutation);
            var failedValidation = await ValidateCompilerImpactAsync(
                target,
                workspaceSelector,
                invalidSnapshot);

            var rejectedCommit = await CommitAsync(
                target,
                workspaceSelector,
                invalidSnapshot);

            var bytesAfterRejection = await File.ReadAllBytesAsync(
                documentPath,
                TestContext.Current.CancellationToken);

            var correctedMutation = await StageExternalMutationAsync(
                target,
                workspaceSelector,
                invalidSnapshot,
                "Class1.cs",
                "Class1 : MissingBase",
                "ValidatedClass");

            var correctedSnapshot = AcceptanceProtocol.GetSnapshot(correctedMutation);
            var successfulValidation = await ValidateCompilerImpactAsync(
                target,
                workspaceSelector,
                correctedSnapshot);

            var successfulCommit = await CommitAsync(
                target,
                workspaceSelector,
                correctedSnapshot);

            var persistedSource = await File.ReadAllTextAsync(
                documentPath,
                TestContext.Current.CancellationToken);

            var failedValidationData = AcceptanceProtocol.GetSuccessData(failedValidation);
            failedValidationData.GetProperty("isComplete").GetBoolean().Should().BeTrue();
            failedValidationData.GetProperty("succeeded").GetBoolean().Should().BeFalse();
            failedValidationData.GetProperty("introducedErrorCount").GetInt32().Should().BeGreaterThan(0);
            rejectedCommit.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(rejectedCommit).GetProperty("code").GetString().Should().Be("NewCompilerErrors");
            bytesAfterRejection.Should().Equal(originalBytes);
            AcceptanceProtocol.GetSuccessData(successfulValidation)
                .GetProperty("succeeded")
                .GetBoolean()
                .Should()
                .BeTrue();

            successfulCommit.IsError.Should().NotBeTrue();
            persistedSource.Should().Contain("ValidatedClass");
            persistedSource.Should().NotContain("MissingBase");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_DependencyBreak_WHEN_RepeatingPublishedCompilerValidation_THEN_ShouldDetectDependantWithinPerformanceBudget()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            workspaceAsset: AcceptanceWorkspaceAsset.SolutionHierarchy,
            additionalArguments: ["--commit-validation", "no-new-compiler-errors"],
            pluginAssets: [AcceptancePluginAsset.HostMutation]);

        try
        {
            var dependencyPath = Path.Combine(target.WorkspaceRoot, "Lib", "MessageFormatter.cs");
            var originalBytes = await File.ReadAllBytesAsync(
                dependencyPath,
                TestContext.Current.CancellationToken);

            var workspace = await OpenWorkspaceAsync(
                target,
                Path.Combine(target.WorkspaceRoot, "Sample.slnx"));

            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);
            var mutation = await StageExternalMutationAsync(
                target,
                workspaceSelector,
                workspace.CreateSnapshot(transactionRevision: 0),
                Path.Combine("Lib", "MessageFormatter.cs"),
                "string Format",
                "int Format");

            var stagedSnapshot = AcceptanceProtocol.GetSnapshot(mutation);
            var previewStopwatch = Stopwatch.StartNew();
            var preview = await PreviewAsync(target, workspaceSelector);

            previewStopwatch.Stop();
            var coldStopwatch = Stopwatch.StartNew();
            var coldValidation = await ValidateCompilerImpactAsync(
                target,
                workspaceSelector,
                stagedSnapshot);

            coldStopwatch.Stop();
            var warmStopwatch = Stopwatch.StartNew();
            var warmValidation = await ValidateCompilerImpactAsync(
                target,
                workspaceSelector,
                stagedSnapshot);

            warmStopwatch.Stop();
            var rejectedCommit = await CommitAsync(
                target,
                workspaceSelector,
                stagedSnapshot);

            var persistedBytes = await File.ReadAllBytesAsync(
                dependencyPath,
                TestContext.Current.CancellationToken);

            var coldData = AcceptanceProtocol.GetSuccessData(coldValidation);
            var warmData = AcceptanceProtocol.GetSuccessData(warmValidation);
            var coldDurationMilliseconds = coldData.GetProperty("durationMilliseconds").GetInt64();
            var warmDurationMilliseconds = warmData.GetProperty("durationMilliseconds").GetInt64();
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Transaction preview duration: observed={previewStopwatch.ElapsedMilliseconds}ms.");

            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Compiler validation cold duration: observed={coldStopwatch.ElapsedMilliseconds}ms, reported={coldDurationMilliseconds}ms.");

            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Compiler validation repeated duration: observed={warmStopwatch.ElapsedMilliseconds}ms, reported={warmDurationMilliseconds}ms.");

            preview.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(preview)
                .GetProperty("documents")
                .EnumerateArray()
                .Should()
                .ContainSingle();

            coldData.GetProperty("succeeded").GetBoolean().Should().BeFalse();
            coldData.GetProperty("introducedDiagnostics")
                .EnumerateArray()
                .Should()
                .Contain(item => item.GetProperty("id").GetString() == "CS0738");

            coldData.GetProperty("projects")
                .EnumerateArray()
                .Should()
                .Contain(item => IsAppProjectWithIntroducedErrors(item));

            warmData.GetProperty("introducedErrorCount").GetInt32()
                .Should().Be(coldData.GetProperty("introducedErrorCount").GetInt32());

            coldStopwatch.ElapsedMilliseconds.Should().BeLessThan(
                _compilerValidationPerformanceBudgetMilliseconds,
                $"cold validation reported {coldDurationMilliseconds}ms");

            warmStopwatch.ElapsedMilliseconds.Should().BeLessThan(
                _compilerValidationPerformanceBudgetMilliseconds,
                $"repeated validation reported {warmDurationMilliseconds}ms");

            coldDurationMilliseconds.Should().BeLessThan(_compilerValidationPerformanceBudgetMilliseconds);
            warmDurationMilliseconds.Should().BeLessThan(_compilerValidationPerformanceBudgetMilliseconds);
            rejectedCommit.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(rejectedCommit).GetProperty("code").GetString().Should().Be("NewCompilerErrors");
            persistedBytes.Should().Equal(originalBytes);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_ExternalMutationPackage_WHEN_StagingAndRollingBack_THEN_ShouldChangeOnlyTheStagedSolution()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets: [AcceptancePluginAsset.HostMutation]);

        try
        {
            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            var originalBytes = await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken);
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"));
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);

            var mutationResult = await target.CallToolAsync(
                "host-valid-mutation",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                    ["summary"] = "Rename the acceptance fixture type.",
                    ["relativeDocumentPath"] = "Class1.cs",
                    ["searchText"] = "Class1",
                    ["replacementText"] = "ExternallyRenamedClass",
                },
                TestContext.Current.CancellationToken);

            mutationResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(mutationResult).GetProperty("staged").GetBoolean().Should().BeTrue();
            (await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken)).Should().Equal(originalBytes);

            var stagedSearch = await SearchSymbolsAsync(target, workspaceSelector, "ExternallyRenamedClass");
            GetSymbolItems(stagedSearch).Should().ContainSingle();

            var previewResult = await PreviewAsync(target, workspaceSelector);
            AcceptanceProtocol.GetSuccessData(previewResult)
                .GetProperty("documents")
                .EnumerateArray()
                .Should()
                .ContainSingle(change => change.GetProperty("document").GetProperty("path").GetString() == "Class1.cs");

            await RollbackAsync(target, workspaceSelector);

            (await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken)).Should().Equal(originalBytes);
            GetSymbolItems(await SearchSymbolsAsync(target, workspaceSelector, "Class1")).Should().ContainSingle();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_NoChangeExternalMutation_WHEN_InvokingTool_THEN_ShouldRetainRevisionZero()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets: [AcceptancePluginAsset.HostMutation]);

        try
        {
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"));
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);

            var mutationResult = await target.CallToolAsync(
                "host-valid-mutation",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                    ["summary"] = "Return the current immutable solution.",
                },
                TestContext.Current.CancellationToken);

            mutationResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(mutationResult).GetProperty("staged").GetBoolean().Should().BeFalse();

            var preview = AcceptanceProtocol.GetSuccessData(await PreviewAsync(target, workspaceSelector));
            preview.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(0);
            preview.GetProperty("transaction").GetProperty("revisionCount").GetInt32().Should().Be(0);
            preview.GetProperty("documents").GetArrayLength().Should().Be(0);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_RejectedAndTwoSuccessfulMutations_WHEN_TraversingHistory_THEN_ShouldExposeTheSelectedStagedRevision()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            var originalBytes = await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken);
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"));
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);

            var rejectedResult = await RenameAsync(
                target,
                workspaceSelector,
                "T:Sample.MissingClass",
                "RejectedClass",
                workspace.CreateSnapshot(transactionRevision: 0));

            rejectedResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(rejectedResult).GetProperty("code").GetString().Should().Be("SymbolNotFound");
            AcceptanceProtocol.GetSuccessData(await PreviewAsync(target, workspaceSelector))
                .GetProperty("transaction")
                .GetProperty("revision")
                .GetInt32()
                .Should()
                .Be(0);

            var firstMutation = await RenameAsync(
                target,
                workspaceSelector,
                "T:Sample.Class1",
                "Class2",
                workspace.CreateSnapshot(transactionRevision: 0));
            firstMutation.IsError.Should().NotBeTrue();
            GetSymbolItems(await SearchSymbolsAsync(target, workspaceSelector, "Class2")).Should().ContainSingle();

            var secondMutation = await RenameAsync(
                target,
                workspaceSelector,
                "T:Sample.Class2",
                "Class3",
                AcceptanceProtocol.GetSnapshot(firstMutation));
            secondMutation.IsError.Should().NotBeTrue();

            var revisionTwo = AcceptanceProtocol.GetSuccessData(await PreviewAsync(target, workspaceSelector))
                .GetProperty("transaction");
            revisionTwo.GetProperty("revision").GetInt32().Should().Be(2);
            revisionTwo.GetProperty("revisionCount").GetInt32().Should().Be(2);

            var undoResult = await MoveHistoryAsync(
                target,
                workspaceSelector,
                "Undo",
                AcceptanceProtocol.GetSnapshot(secondMutation));
            var undoTransaction = AcceptanceProtocol.GetSuccessData(undoResult).GetProperty("transaction");
            undoTransaction.GetProperty("revision").GetInt32().Should().Be(1);
            undoTransaction.GetProperty("canRedo").GetBoolean().Should().BeTrue();
            GetSymbolItems(await SearchSymbolsAsync(target, workspaceSelector, "Class2")).Should().ContainSingle();

            var redoResult = await MoveHistoryAsync(
                target,
                workspaceSelector,
                "Redo",
                AcceptanceProtocol.GetSnapshot(undoResult));
            var redoTransaction = AcceptanceProtocol.GetSuccessData(redoResult).GetProperty("transaction");
            redoTransaction.GetProperty("revision").GetInt32().Should().Be(2);
            redoTransaction.GetProperty("canUndo").GetBoolean().Should().BeTrue();
            GetSymbolItems(await SearchSymbolsAsync(target, workspaceSelector, "Class3")).Should().ContainSingle();

            await RollbackAsync(target, workspaceSelector);

            (await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken)).Should().Equal(originalBytes);
            GetSymbolItems(await SearchSymbolsAsync(target, workspaceSelector, "Class1")).Should().ContainSingle();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_DiscardedAndReplacementBranchesShareRevision_WHEN_UsingDiscardedSnapshot_THEN_ShouldRejectSnapshot()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"));
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);

            var discardedBranch = await RenameAsync(
                target,
                workspaceSelector,
                "T:Sample.Class1",
                "DiscardedBranchClass",
                workspace.CreateSnapshot(transactionRevision: 0));
            discardedBranch.IsError.Should().NotBeTrue();
            var discardedSnapshot = AcceptanceProtocol.GetSnapshot(discardedBranch);

            var undoResult = await MoveHistoryAsync(
                target,
                workspaceSelector,
                "Undo",
                discardedSnapshot);
            undoResult.IsError.Should().NotBeTrue();

            var replacementBranch = await RenameAsync(
                target,
                workspaceSelector,
                "T:Sample.Class1",
                "ReplacementBranchClass",
                AcceptanceProtocol.GetSnapshot(undoResult));
            replacementBranch.IsError.Should().NotBeTrue();
            var replacementSnapshot = AcceptanceProtocol.GetSnapshot(replacementBranch);

            discardedSnapshot["transactionRevision"].Should().Be(replacementSnapshot["transactionRevision"]);
            discardedSnapshot["snapshotId"].Should().NotBe(replacementSnapshot["snapshotId"]);

            var staleResult = await RenameAsync(
                target,
                workspaceSelector,
                "T:Sample.ReplacementBranchClass",
                "StaleRename",
                discardedSnapshot);

            staleResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(staleResult).GetProperty("code").GetString().Should().Be("SnapshotMismatch");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_TwoWorkspaces_WHEN_TransferringTransactionOwnership_THEN_ShouldPublishTheCurrentOwner()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var secondRoot = target.CopyWorkspaceAsset(AcceptanceWorkspaceAsset.SdkProject, "transaction-owner");
            var first = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"), "first");
            var second = await OpenWorkspaceAsync(target, Path.Combine(secondRoot, "Sample.csproj"), "second", secondRoot);
            var firstSelector = first.CreateSelector();
            var secondSelector = second.CreateSelector();

            await StartTransactionAsync(target, firstSelector);
            var firstList = AcceptanceProtocol.GetSuccessData(await target.CallToolAsync(
                "workspace-list",
                new Dictionary<string, object?>(),
                TestContext.Current.CancellationToken));
            firstList.GetProperty("transactionOwnerWorkspaceId").GetGuid().Should().Be(first.WorkspaceId);

            var blockedStart = await target.CallToolAsync(
                "transaction-start",
                new Dictionary<string, object?>
                {
                    ["workspace"] = secondSelector,
                },
                TestContext.Current.CancellationToken);
            blockedStart.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(blockedStart).GetProperty("code").GetString().Should().Be("TransactionOwnedByWorkspace");

            await RollbackAsync(target, firstSelector);
            await StartTransactionAsync(target, secondSelector);

            var secondList = AcceptanceProtocol.GetSuccessData(await target.CallToolAsync(
                "workspace-list",
                new Dictionary<string, object?>(),
                TestContext.Current.CancellationToken));
            secondList.GetProperty("transactionOwnerWorkspaceId").GetGuid().Should().Be(second.WorkspaceId);
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
        string? alias = null,
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

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> RenameAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        string documentationCommentId,
        string newName,
        IReadOnlyDictionary<string, object?> expectedSnapshot)
    {
        return await target.CallToolAsync(
            "rename-symbol",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["symbol"] = new Dictionary<string, object?>
                {
                    ["documentationCommentId"] = documentationCommentId,
                },
                ["newName"] = newName,
                ["expectedSnapshot"] = expectedSnapshot,
            },
            TestContext.Current.CancellationToken);
    }

    private static Task<ModelContextProtocol.Protocol.CallToolResult> StageExternalMutationAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        IReadOnlyDictionary<string, object?> expectedSnapshot,
        string relativeDocumentPath,
        string searchText,
        string replacementText)
    {
        return target.CallToolAsync(
            "host-valid-mutation",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["expectedSnapshot"] = expectedSnapshot,
                ["summary"] = "Stage compiler validation acceptance scenario.",
                ["relativeDocumentPath"] = relativeDocumentPath,
                ["searchText"] = searchText,
                ["replacementText"] = replacementText,
            },
            TestContext.Current.CancellationToken);
    }

    private static bool IsAppProjectWithIntroducedErrors(System.Text.Json.JsonElement item)
    {
        var project = item.GetProperty("project").GetString();
        return project is not null
            && project.EndsWith("App.csproj", StringComparison.Ordinal)
            && item.GetProperty("introducedErrorCount").GetInt32() > 0;
    }

    private static Task<ModelContextProtocol.Protocol.CallToolResult> ValidateCompilerImpactAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        IReadOnlyDictionary<string, object?> expectedSnapshot)
    {
        return target.CallToolAsync(
            "transaction-validate",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["expectedSnapshot"] = expectedSnapshot,
            },
            TestContext.Current.CancellationToken);
    }

    private static Task<ModelContextProtocol.Protocol.CallToolResult> CommitAsync(
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

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> MoveHistoryAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        string direction,
        IReadOnlyDictionary<string, object?> expectedSnapshot)
    {
        return await target.CallToolAsync(
            "transaction-history",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["direction"] = direction,
                ["expectedSnapshot"] = expectedSnapshot,
            },
            TestContext.Current.CancellationToken);
    }

    private static Task<ModelContextProtocol.Protocol.CallToolResult> PreviewAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector)
    {
        return target.CallToolAsync(
            "transaction-preview",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task RollbackAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector)
    {
        var result = await target.CallToolAsync(
            "transaction-rollback",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().NotBeTrue();
    }

    private static Task<ModelContextProtocol.Protocol.CallToolResult> SearchSymbolsAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        string query)
    {
        return target.CallToolAsync(
            "search-symbols",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["query"] = query,
            },
            TestContext.Current.CancellationToken);
    }

    private static System.Text.Json.JsonElement.ArrayEnumerator GetSymbolItems(
        ModelContextProtocol.Protocol.CallToolResult result)
    {
        result.IsError.Should().NotBeTrue();
        return AcceptanceProtocol.GetSuccessData(result)
            .GetProperty("symbols")
            .GetProperty("items")
            .EnumerateArray();
    }
}
