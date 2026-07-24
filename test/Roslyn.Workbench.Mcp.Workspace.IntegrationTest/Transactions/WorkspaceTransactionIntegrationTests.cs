using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceTransactionIntegrationTests
{
    [Fact]
    public async Task GIVEN_TwoOpenedWorkspaces_WHEN_StartingTransactionOnSecondWorkspace_THEN_ShouldRejectUntilOwnerRollsBack()
    {
        using var fixtureA = TestWorkspaceFixture.Create();
        using var fixtureB = TestWorkspaceFixture.Create();
        await using var target = fixtureA.CreateWorkspace();

        var openA = await target.OpenAsync(
            fixtureA.ProjectPath,
            TestContext.Current.CancellationToken,
            alias: "alpha");

        var openB = await target.OpenAsync(
            fixtureB.ProjectPath,
            TestContext.Current.CancellationToken,
            alias: "beta");

        var startA = await target.StartTransactionAsync(
            TestContext.Current.CancellationToken,
            workspaceId: openA.Data!.Workspace.WorkspaceId);

        var startBRejected = await target.StartTransactionAsync(
            TestContext.Current.CancellationToken,
            workspaceId: openB.Data!.Workspace.WorkspaceId);

        var rollbackA = await target.RollbackTransactionAsync(
            TestContext.Current.CancellationToken,
            workspaceId: openA.Data.Workspace.WorkspaceId);

        var startBAfterRollback = await target.StartTransactionAsync(
            TestContext.Current.CancellationToken,
            workspaceId: openB.Data.Workspace.WorkspaceId);

        startA.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        startBRejected.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        startBRejected.Error!.Code.Should().Be("TransactionOwnedByWorkspace");
        startBRejected.Error.Message.Should().Contain("alpha");
        rollbackA.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        startBAfterRollback.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        startBAfterRollback.Context.WorkspaceId.Should().Be(openB.Data.Workspace.WorkspaceId);
    }

    [Fact]
    public async Task GIVEN_ReadyWorkspace_WHEN_StartingTransaction_THEN_ShouldReportActiveTransactionCapabilities()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        var openResult = await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        var result = await target.StartTransactionAsync(TestContext.Current.CancellationToken);
        var status = await target.GetStatusAsync(TestContext.Current.CancellationToken);

        result.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Context.WorkspaceEpoch.Should().Be(openResult.Context.WorkspaceEpoch);
        result.Context.TransactionRevision.Should().Be(0);
        result.Data!.Transaction.Revision.Should().Be(0);
        result.Data.Transaction.RevisionCount.Should().Be(0);
        status.Data!.State.Should().Be(WorkspaceLifecycleState.TransactionActive);
        status.Data.Transaction!.CanMutate.Should().BeTrue();
        status.Data.Transaction.CanCommit.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_ActiveTransaction_WHEN_ExecutingMutationTool_THEN_ShouldStageRevisionAndPreviewChanges()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(TestContext.Current.CancellationToken);
        var result = await StageMutationAsync(target);
        var preview = await target.PreviewTransactionAsync(
            TestContext.Current.CancellationToken,
            document: new Contracts.Selectors.DocumentSelector
            {
                Path = "Class1.cs",
            },
            includeDiff: true);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Transaction!.Revision.Should().Be(1);
        result.Data.Operation.Should().Be("test-stage-mutation");
        preview.Data!.Transaction!.Revision.Should().Be(1);
        preview.Data.Documents.Should().ContainSingle();
        preview.Data.Diff.Should().NotBeNull();
        preview.Data.Diff!.Hunks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GIVEN_StagedTransaction_WHEN_MovingHistoryBackwardAndForward_THEN_ShouldUpdateCurrentRevision()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);
        var preview = await target.PreviewTransactionAsync(TestContext.Current.CancellationToken);

        var undo = await target.MoveTransactionHistoryAsync(
            TransactionHistoryDirection.Undo,
            TestContext.Current.CancellationToken,
            expectedSnapshot: new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = preview.Context.WorkspaceId,
                WorkspaceEpoch = preview.Context.WorkspaceEpoch!.Value,
                TransactionRevision = preview.Context.TransactionRevision,
            });

        var redo = await target.MoveTransactionHistoryAsync(
            TransactionHistoryDirection.Redo,
            TestContext.Current.CancellationToken,
            expectedSnapshot: new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = undo.Context.WorkspaceId,
                WorkspaceEpoch = undo.Context.WorkspaceEpoch!.Value,
                TransactionRevision = undo.Context.TransactionRevision,
            });

        undo.Data!.Transaction!.Revision.Should().Be(0);
        undo.Data.Transaction.CanRedo.Should().BeTrue();
        redo.Data!.Transaction!.Revision.Should().Be(1);
        redo.Data.Transaction.CanUndo.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_StagedTransaction_WHEN_RollingBack_THEN_ShouldClearTransactionAndReturnReady()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);

        var rollback = await target.RollbackTransactionAsync(TestContext.Current.CancellationToken);
        var status = await target.GetStatusAsync(TestContext.Current.CancellationToken);

        rollback.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        rollback.Data!.State.Should().Be(TransactionRollbackState.Ready);
        status.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        status.Data.Transaction.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_StagedTransaction_WHEN_Committing_THEN_ShouldPersistDocumentChangesToDisk()
    {
        using var fixture = TestWorkspaceFixture.Create();
        var originalDocumentBytes = await File.ReadAllBytesAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(TestContext.Current.CancellationToken);
        await StageMutationAsync(target);
        var preview = await target.PreviewTransactionAsync(TestContext.Current.CancellationToken);

        var commit = await target.CommitTransactionAsync(
            TestContext.Current.CancellationToken,
            expectedSnapshot: new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = preview.Context.WorkspaceId,
                WorkspaceEpoch = preview.Context.WorkspaceEpoch!.Value,
                TransactionRevision = preview.Context.TransactionRevision,
            });

        var status = await target.GetStatusAsync(TestContext.Current.CancellationToken);
        var text = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        using var pristineFixture = TestWorkspaceFixture.Create();
        var pristineDocumentBytes = await File.ReadAllBytesAsync(pristineFixture.DocumentPath, TestContext.Current.CancellationToken);

        commit.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        commit.Data!.Committed.Should().BeTrue();
        status.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        text.Should().Contain("TransactionMarker");
        pristineFixture.WorkspaceRoot.Should().NotBe(fixture.WorkspaceRoot);
        pristineFixture.StateRoot.Should().NotBe(fixture.StateRoot);
        pristineDocumentBytes.Should().Equal(originalDocumentBytes);
    }

    [Fact]
    public async Task GIVEN_MultipleStagedDocuments_WHEN_Committing_THEN_ShouldPersistEveryFileAndRemoveJournal()
    {
        using var fixture = TestWorkspaceFixture.Create();
        var secondPath = Path.Combine(Path.GetDirectoryName(fixture.ProjectPath)!, "Class2.cs");
        await File.WriteAllTextAsync(secondPath, "namespace Sample; public sealed class Class2 { }", TestContext.Current.CancellationToken);
        using var stateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-transaction-tests");
        await using var target = ComponentWorkspace.Create(new ComponentWorkspaceOptions { StateDirectory = stateDirectory.DirectoryPath });
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(TestContext.Current.CancellationToken);
        await StageMutationAsync(target, stageEveryDocument: true);

        var commit = await target.CommitTransactionAsync(TestContext.Current.CancellationToken);

        commit.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        (await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken)).Should().Contain("TransactionMarker");
        (await File.ReadAllTextAsync(secondPath, TestContext.Current.CancellationToken)).Should().Contain("TransactionMarker");
        Directory.Exists(Path.Combine(stateDirectory.DirectoryPath, "recovery")).Should().BeTrue();
        Directory.EnumerateFileSystemEntries(Path.Combine(stateDirectory.DirectoryPath, "recovery")).Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_UnicodeEncodedDocument_WHEN_Committing_THEN_ShouldPreserveDocumentEncoding()
    {
        using var fixture = TestWorkspaceFixture.Create();
        var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        await File.WriteAllTextAsync(fixture.DocumentPath, """
            namespace Sample;

            public sealed class Class1
            {
            }
            """, encoding, TestContext.Current.CancellationToken);

        await using var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);
        var preview = await target.PreviewTransactionAsync(TestContext.Current.CancellationToken);

        var commit = await target.CommitTransactionAsync(
            TestContext.Current.CancellationToken,
            expectedSnapshot: new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = preview.Context.WorkspaceId,
                WorkspaceEpoch = preview.Context.WorkspaceEpoch!.Value,
                TransactionRevision = preview.Context.TransactionRevision,
            });

        var text = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        var bytes = await File.ReadAllBytesAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);

        commit.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        bytes.Should().StartWith(encoding.GetPreamble());
        text.Should().Contain("TransactionMarker");
    }

    [Fact]
    public async Task GIVEN_ChangedWorkspaceInputDuringTransaction_WHEN_GettingStatus_THEN_ShouldTransitionToTransactionConflicted()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);
        await using var observer = fixture.CreateWorkspace();
        await observer.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(fixture.DocumentPath, Environment.NewLine + "class ExternalChange { }", TestContext.Current.CancellationToken);

        var status = await target.GetStatusAsync(TestContext.Current.CancellationToken);
        var observedState = await ObserveOtherInstanceStateAsync(
            observer,
            WorkspaceLifecycleState.TransactionConflicted);

        status.Data!.State.Should().Be(WorkspaceLifecycleState.TransactionConflicted);
        status.Data.Transaction!.CanMutate.Should().BeFalse();
        observedState.Should().Be(WorkspaceLifecycleState.TransactionConflicted);
    }

    private static async Task<ComponentWorkspace> CreateCoordinatorWithOneStagedRevisionAsync(TestWorkspaceFixture fixture)
    {
        var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(TestContext.Current.CancellationToken);
        await StageMutationAsync(target);

        return target;
    }

    private static async Task<PluginExecutionResult<MutationData>> StageMutationAsync(
        ComponentWorkspace target,
        bool stageEveryDocument = false)
    {
        await using var lease = target.CreateMutationContext(new StageMutationRequest(), TestContext.Current.CancellationToken);
        lease.HasFailure.Should().BeFalse();
        var candidateSolution = lease.Context!.CurrentSolution;
        var documents = candidateSolution.Projects.SelectMany(static project => project.Documents);
        if (!stageEveryDocument)
        {
            documents = documents.Where(static document => document.Name == "Class1.cs");
        }

        foreach (var document in documents)
        {
            var sourceText = await document.GetTextAsync(TestContext.Current.CancellationToken);
            candidateSolution = candidateSolution.WithDocumentText(
                document.Id,
                sourceText.WithChanges(
                [
                    new Microsoft.CodeAnalysis.Text.TextChange(
                        new Microsoft.CodeAnalysis.Text.TextSpan(sourceText.Length, 0),
                        Environment.NewLine + "public sealed class TransactionMarker { }" + Environment.NewLine),
                ]));
        }

        return await lease.StageAsync(
            "test-stage-mutation",
            new MutationCandidate
            {
                CandidateSolution = candidateSolution,
                Summary = "Stage transaction marker.",
            },
            [],
            [],
            TestContext.Current.CancellationToken);
    }

    private static async ValueTask<WorkspaceLifecycleState?> ObserveOtherInstanceStateAsync(
        ComponentWorkspace observer,
        WorkspaceLifecycleState expectedState)
    {
        WorkspaceLifecycleState? observedState = null;
        for (var attempt = 0; attempt < 1000 && observedState != expectedState; attempt++)
        {
            var result = await observer.GetStatusAsync(TestContext.Current.CancellationToken);
            observedState = result.Data?.Instances
                .Select(static instance => instance.WorkspaceState)
                .FirstOrDefault(state => state == expectedState);

            await Task.Yield();
        }

        return observedState;
    }

    private sealed record StageMutationRequest : WorkspaceBoundRequest;
}
