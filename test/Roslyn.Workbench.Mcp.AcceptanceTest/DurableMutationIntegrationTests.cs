namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class DurableMutationIntegrationTests
{
    private const int _refactoringKind = 2;

    private const UnixFileMode _preservedUnixFileMode = UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead;

    [Fact]
    public async Task GIVEN_DiscoveredCreateAndReplaceCodeAction_WHEN_CommittingAndRestarting_THEN_ShouldPromoteCleanDurableState()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            workspaceAsset: AcceptanceWorkspaceAsset.InspectionSample);

        try
        {
            var sourcePath = Path.Combine(target.WorkspaceRoot, "Formatting.cs");
            var createdPath = Path.Combine(target.WorkspaceRoot, "AlphaCycle.cs");
            var originalSource = await File.ReadAllBytesAsync(sourcePath, TestContext.Current.CancellationToken);
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"));
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);
            var locations = new AcceptanceLocationSelectorFactory(target.WorkspaceRoot);

            var listResult = await target.CallToolAsync(
                "list-code-actions",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["document"] = AcceptanceLocationSelectorFactory.CreateDocument("Formatting.cs"),
                    ["range"] = locations.CreateRange(
                        "Formatting.cs",
                        "public sealed class AlphaCycle",
                        "public sealed class AlphaCycle"),
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                    ["kinds"] = _refactoringKind,
                },
                TestContext.Current.CancellationToken);

            listResult.IsError.Should().NotBeTrue(
                listResult.IsError == true
                    ? AcceptanceProtocol.GetError(listResult).GetRawText()
                    : string.Empty);
            var actions = AcceptanceProtocol.GetSuccessData(listResult)
                .GetProperty("actions")
                .GetProperty("items")
                .EnumerateArray();
            var moveTypeAction = actions.Single(static action =>
                action.GetProperty("title").GetString()?.Contains("Move type to", StringComparison.Ordinal) == true);

            var mutationResult = await target.CallToolAsync(
                "stage-code-action",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["actionId"] = moveTypeAction.GetProperty("actionId").GetGuid(),
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                },
                TestContext.Current.CancellationToken);

            mutationResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(mutationResult).GetProperty("staged").GetBoolean().Should().BeTrue();
            File.Exists(createdPath).Should().BeFalse();
            (await File.ReadAllBytesAsync(sourcePath, TestContext.Current.CancellationToken)).Should().Equal(originalSource);

            var preview = AcceptanceProtocol.GetSuccessData(await PreviewAsync(target, workspaceSelector));
            var changes = preview.GetProperty("documents").EnumerateArray().ToArray();
            changes.Should().ContainSingle(change =>
                change.GetProperty("changeKind").GetString() == "Added"
                && change.GetProperty("document").GetProperty("path").GetString() == "AlphaCycle.cs");
            changes.Should().ContainSingle(change =>
                change.GetProperty("changeKind").GetString() == "Modified"
                && change.GetProperty("document").GetProperty("path").GetString() == "Formatting.cs");

            var commitResult = await CommitAsync(
                target,
                workspaceSelector,
                workspace.CreateSnapshot(transactionRevision: 1));
            commitResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(commitResult).GetProperty("committed").GetBoolean().Should().BeTrue();
            File.Exists(createdPath).Should().BeTrue();

            var committedSource = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);
            committedSource.Should().NotContain("public sealed class AlphaCycle");
            (await File.ReadAllTextAsync(createdPath, TestContext.Current.CancellationToken))
                .Should()
                .Contain("public sealed class AlphaCycle");

            await target.RestartAsync(TestContext.Current.CancellationToken);
            var reopenedWorkspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"));
            var searchResult = await SearchSymbolsAsync(target, reopenedWorkspace.CreateSelector(), "AlphaCycle");
            GetSymbolItems(searchResult).Should().ContainSingle();
            await AssertNoRecoveryAsync(target);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_UnixSourceWithDistinctivePermissions_WHEN_PublishedHostCommitsRename_THEN_ShouldPreservePermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken);

        try
        {
            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            File.SetUnixFileMode(documentPath, _preservedUnixFileMode);
            var workspace = await OpenWorkspaceAsync(
                target,
                Path.Combine(target.WorkspaceRoot, "Sample.csproj"));

            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);
            var renameResult = await RenameAsync(
                target,
                workspaceSelector,
                "T:Sample.Class1",
                "PermissionPreservedClass",
                workspace.CreateSnapshot(transactionRevision: 0));

            renameResult.IsError.Should().NotBeTrue();
            var commitResult = await CommitAsync(
                target,
                workspaceSelector,
                workspace.CreateSnapshot(transactionRevision: 1));

            commitResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(commitResult).GetProperty("committed").GetBoolean().Should().BeTrue();
            (await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken))
                .Should()
                .Contain("PermissionPreservedClass");

            File.GetUnixFileMode(documentPath).Should().Be(_preservedUnixFileMode);
            await AssertNoRecoveryAsync(target);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_SolutionWideRename_WHEN_Committing_THEN_ShouldReplaceTheExactMultiFileSet()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            workspaceAsset: AcceptanceWorkspaceAsset.SolutionHierarchy);

        try
        {
            var appPath = Path.Combine(target.WorkspaceRoot, "App", "AppFormatter.cs");
            var libraryPath = Path.Combine(target.WorkspaceRoot, "Lib", "MessageFormatter.cs");
            var originalApp = await File.ReadAllBytesAsync(appPath, TestContext.Current.CancellationToken);
            var originalLibrary = await File.ReadAllBytesAsync(libraryPath, TestContext.Current.CancellationToken);
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.slnx"));
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);

            var renameResult = await RenameAsync(
                target,
                workspaceSelector,
                "T:Sample.IMessageFormatter",
                "ITextFormatter",
                workspace.CreateSnapshot(transactionRevision: 0));

            renameResult.IsError.Should().NotBeTrue();
            (await File.ReadAllBytesAsync(appPath, TestContext.Current.CancellationToken)).Should().Equal(originalApp);
            (await File.ReadAllBytesAsync(libraryPath, TestContext.Current.CancellationToken)).Should().Equal(originalLibrary);

            var preview = AcceptanceProtocol.GetSuccessData(await PreviewAsync(target, workspaceSelector));
            var changedPaths = preview
                .GetProperty("documents")
                .EnumerateArray()
                .Select(static change => change.GetProperty("document").GetProperty("path").GetString())
                .ToArray();
            changedPaths.Should().BeEquivalentTo(["App/AppFormatter.cs", "Lib/MessageFormatter.cs"]);

            var commitResult = await CommitAsync(
                target,
                workspaceSelector,
                workspace.CreateSnapshot(transactionRevision: 1));
            commitResult.IsError.Should().NotBeTrue();
            (await File.ReadAllTextAsync(appPath, TestContext.Current.CancellationToken)).Should().Contain("ITextFormatter");
            (await File.ReadAllTextAsync(libraryPath, TestContext.Current.CancellationToken)).Should().Contain("ITextFormatter");
            await AssertNoRecoveryAsync(target);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_LinkedMultiTargetDocument_WHEN_CommittingRename_THEN_ShouldWriteThePhysicalTargetOnce()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            workspaceAsset: AcceptanceWorkspaceAsset.MultiTargetLinked);

        try
        {
            var documentPath = Path.Combine(target.WorkspaceRoot, "Shared", "SharedFormatter.cs");
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.slnx"));
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);

            var renameResult = await target.CallToolAsync(
                "rename-symbol",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["symbol"] = new Dictionary<string, object?>
                    {
                        ["project"] = new Dictionary<string, object?>
                        {
                            ["path"] = "MultiTarget/MultiTarget.csproj",
                            ["targetFramework"] = "net10.0",
                        },
                        ["documentationCommentId"] = "T:Shared.SharedFormatter",
                    },
                    ["newName"] = "RenamedSharedFormatter",
                    ["renameFile"] = false,
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                },
                TestContext.Current.CancellationToken);

            renameResult.IsError.Should().NotBeTrue(
                "the linked-target rename should stage successfully; response: {0}",
                renameResult.StructuredContent);
            var preview = AcceptanceProtocol.GetSuccessData(await PreviewAsync(target, workspaceSelector));
            preview.GetProperty("documents").EnumerateArray()
                .Select(static change => change.GetProperty("document").GetProperty("path").GetString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Should()
                .ContainSingle("Shared/SharedFormatter.cs");

            var commitResult = await CommitAsync(
                target,
                workspaceSelector,
                workspace.CreateSnapshot(transactionRevision: 1));
            commitResult.IsError.Should().NotBeTrue();

            var committedText = await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken);
            committedText.Should().Contain("class RenamedSharedFormatter");
            committedText.Should().NotContain("class SharedFormatter");
            Directory.EnumerateFiles(target.WorkspaceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
                .Should()
                .ContainSingle(documentPath);
            await AssertNoRecoveryAsync(target);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_ExternalPreWriteDrift_WHEN_Committing_THEN_ShouldConflictWithoutOverwritingExternalBytes()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            var workspace = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"));
            var workspaceSelector = workspace.CreateSelector();
            await StartTransactionAsync(target, workspaceSelector);

            var renameResult = await RenameAsync(
                target,
                workspaceSelector,
                "T:Sample.Class1",
                "RenamedClass",
                workspace.CreateSnapshot(transactionRevision: 0));
            renameResult.IsError.Should().NotBeTrue();

            var externalText = await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken)
                + "\r\n// External acceptance change.\r\n";
            await File.WriteAllTextAsync(documentPath, externalText, TestContext.Current.CancellationToken);

            var commitResult = await CommitAsync(
                target,
                workspaceSelector,
                workspace.CreateSnapshot(transactionRevision: 1));
            commitResult.IsError.Should().BeTrue();
            var error = AcceptanceProtocol.GetError(commitResult);
            error.GetProperty("code").GetString().Should().Be("TransactionConflicted");
            error.TryGetProperty("correlationId", out _).Should().BeFalse();
            var continuation = AcceptanceProtocol.GetContinuation(commitResult);
            continuation.GetProperty("kind").GetString().Should().Be("CallTool");
            continuation.GetProperty("tool").GetString().Should().Be("transaction-rollback");
            continuation.GetProperty("instruction").GetString().Should().NotBeNullOrWhiteSpace();
            (await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken)).Should().Be(externalText);

            var rollbackResult = await target.CallToolAsync(
                "transaction-rollback",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);
            rollbackResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(rollbackResult).GetProperty("state").GetString().Should().Be("WorkspaceOutOfDate");
            (await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken)).Should().Be(externalText);
            await AssertNoRecoveryAsync(target);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<AcceptanceWorkspaceIdentity> OpenWorkspaceAsync(
        AcceptanceProcessFixture target,
        string path)
    {
        var result = await target.CallToolAsync(
            "workspace-open",
            new Dictionary<string, object?>
            {
                ["path"] = path,
                ["workspaceRoot"] = target.WorkspaceRoot,
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

    private static Task<ModelContextProtocol.Protocol.CallToolResult> RenameAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        string documentationCommentId,
        string newName,
        IReadOnlyDictionary<string, object?> expectedSnapshot)
    {
        return target.CallToolAsync(
            "rename-symbol",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["symbol"] = new Dictionary<string, object?>
                {
                    ["documentationCommentId"] = documentationCommentId,
                },
                ["newName"] = newName,
                ["renameFile"] = false,
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

    private static async Task AssertNoRecoveryAsync(AcceptanceProcessFixture target)
    {
        var statusResult = await target.CallToolAsync(
            "server-status",
            new Dictionary<string, object?>
            {
                ["detail"] = "Full",
            },
            TestContext.Current.CancellationToken);

        statusResult.IsError.Should().NotBeTrue();
        AcceptanceProtocol.GetSuccessData(statusResult).GetProperty("recovery").GetArrayLength().Should().Be(0);

        var recoveryRoot = Path.Combine(target.StateRoot, "recovery");
        if (Directory.Exists(recoveryRoot))
        {
            Directory.EnumerateFiles(recoveryRoot, "*", SearchOption.AllDirectories).Should().BeEmpty();
        }
    }
}
