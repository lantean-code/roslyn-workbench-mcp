using System.Text;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceTransactionIntegrationTests
{
    [Fact]
    public async Task GIVEN_TwoOpenedWorkspaces_WHEN_StartingTransactionOnSecondWorkspace_THEN_ShouldRejectUntilOwnerRollsBack()
    {
        await using var fixtureA = await TestWorkspaceFixture.CreateAsync();
        await using var fixtureB = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixtureA.CreateCoordinator();

        var openA = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Alias = "alpha",
            Path = fixtureA.ProjectPath,
        }, TestContext.Current.CancellationToken);
        var openB = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Alias = "beta",
            Path = fixtureB.ProjectPath,
        }, TestContext.Current.CancellationToken);

        var startA = await target.StartTransactionAsync(new TransactionStartRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = openA.Data!.Workspace!.WorkspaceId,
            },
        }, TestContext.Current.CancellationToken);
        var startBRejected = await target.StartTransactionAsync(new TransactionStartRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = openB.Data!.Workspace!.WorkspaceId,
            },
        }, TestContext.Current.CancellationToken);
        var rollbackA = await target.RollbackTransactionAsync(new TransactionRollbackRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = openA.Data!.Workspace!.WorkspaceId,
            },
        }, TestContext.Current.CancellationToken);
        var startBAfterRollback = await target.StartTransactionAsync(new TransactionStartRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = openB.Data!.Workspace!.WorkspaceId,
            },
        }, TestContext.Current.CancellationToken);

        startA.Outcome.Should().Be(ToolOutcome.Succeeded);
        startBRejected.Outcome.Should().Be(ToolOutcome.Rejected);
        startBRejected.Error!.Code.Should().Be("TransactionOwnedByWorkspace");
        startBRejected.Error.Message.Should().Contain("alpha");
        rollbackA.Outcome.Should().Be(ToolOutcome.Succeeded);
        startBAfterRollback.Outcome.Should().Be(ToolOutcome.Succeeded);
        startBAfterRollback.WorkspaceId.Should().Be(openB.Data!.Workspace!.WorkspaceId);
    }

    [Fact]
    public async Task GIVEN_ReadyWorkspace_WHEN_StartingTransaction_THEN_ShouldReportActiveTransactionCapabilities()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixture.CreateCoordinator();
        var openResult = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);

        var result = await target.StartTransactionAsync(new TransactionStartRequest(), TestContext.Current.CancellationToken);
        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.WorkspaceEpoch.Should().Be(openResult.WorkspaceEpoch);
        result.TransactionRevision.Should().Be(0);
        result.Data!.Transaction!.Revision.Should().Be(0);
        result.Data.Transaction.RevisionCount.Should().Be(0);
        status.Data!.State.Should().Be(WorkspaceLifecycleState.TransactionActive);
        status.Data.Transaction!.CanMutate.Should().BeTrue();
        status.Data.Transaction.CanCommit.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_ActiveTransaction_WHEN_ExecutingMutationTool_THEN_ShouldStageRevisionAndPreviewChanges()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(new TransactionStartRequest(), TestContext.Current.CancellationToken);
        var result = await StageMutationAsync(target);
        var preview = await target.PreviewTransactionAsync(new TransactionPreviewRequest
        {
            IncludeDiff = true,
            Document = new Contracts.Selectors.DocumentSelector
            {
                Path = "Class1.cs",
            },
        }, TestContext.Current.CancellationToken);

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
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);
        var preview = await target.PreviewTransactionAsync(new TransactionPreviewRequest(), TestContext.Current.CancellationToken);

        var undo = await target.MoveTransactionHistoryAsync(new TransactionHistoryRequest
        {
            Direction = TransactionHistoryDirection.Undo,
            ExpectedSnapshot = new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = preview.WorkspaceId,
                WorkspaceEpoch = preview.WorkspaceEpoch!.Value,
                TransactionRevision = preview.TransactionRevision,
            },
        }, TestContext.Current.CancellationToken);
        var redo = await target.MoveTransactionHistoryAsync(new TransactionHistoryRequest
        {
            Direction = TransactionHistoryDirection.Redo,
            ExpectedSnapshot = new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = undo.WorkspaceId,
                WorkspaceEpoch = undo.WorkspaceEpoch!.Value,
                TransactionRevision = undo.TransactionRevision,
            },
        }, TestContext.Current.CancellationToken);

        undo.Data!.Transaction!.Revision.Should().Be(0);
        undo.Data.Transaction.CanRedo.Should().BeTrue();
        redo.Data!.Transaction!.Revision.Should().Be(1);
        redo.Data.Transaction.CanUndo.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_StagedTransaction_WHEN_RollingBack_THEN_ShouldClearTransactionAndReturnReady()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);

        var rollback = await target.RollbackTransactionAsync(new TransactionRollbackRequest(), TestContext.Current.CancellationToken);
        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), TestContext.Current.CancellationToken);

        rollback.Outcome.Should().Be(ToolOutcome.Succeeded);
        rollback.Data!.State.Should().Be(TransactionRollbackState.Ready);
        status.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        status.Data.Transaction.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_StagedTransaction_WHEN_Committing_THEN_ShouldPersistDocumentChangesToDisk()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        var originalDocumentBytes = await File.ReadAllBytesAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        await using var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(new TransactionStartRequest(), TestContext.Current.CancellationToken);
        await StageMutationAsync(target);
        var preview = await target.PreviewTransactionAsync(new TransactionPreviewRequest(), TestContext.Current.CancellationToken);

        var commit = await target.CommitTransactionAsync(new TransactionCommitRequest
        {
            ExpectedSnapshot = new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = preview.WorkspaceId,
                WorkspaceEpoch = preview.WorkspaceEpoch!.Value,
                TransactionRevision = preview.TransactionRevision,
            },
        }, TestContext.Current.CancellationToken);
        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), TestContext.Current.CancellationToken);
        var text = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        await using var pristineFixture = await TestWorkspaceFixture.CreateAsync();
        var pristineDocumentBytes = await File.ReadAllBytesAsync(pristineFixture.DocumentPath, TestContext.Current.CancellationToken);

        commit.Outcome.Should().Be(ToolOutcome.Succeeded);
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
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        var secondPath = Path.Combine(Path.GetDirectoryName(fixture.ProjectPath)!, "Class2.cs");
        await File.WriteAllTextAsync(secondPath, "namespace Sample; public sealed class Class2 { }", TestContext.Current.CancellationToken);
        await using var stateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-transaction-tests");
        await using var target = WorkspaceCoordinatorFactory.Create(new WorkspaceRuntimeOptions { StateDirectory = stateDirectory.DirectoryPath });
        await target.OpenAsync(new WorkspaceOpenRequest { Path = fixture.ProjectPath }, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(new TransactionStartRequest(), TestContext.Current.CancellationToken);
        await StageMutationAsync(target, stageEveryDocument: true);

        var commit = await target.CommitTransactionAsync(new TransactionCommitRequest(), TestContext.Current.CancellationToken);

        commit.Outcome.Should().Be(ToolOutcome.Succeeded);
        (await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken)).Should().Contain("TransactionMarker");
        (await File.ReadAllTextAsync(secondPath, TestContext.Current.CancellationToken)).Should().Contain("TransactionMarker");
        Directory.Exists(Path.Combine(stateDirectory.DirectoryPath, "recovery")).Should().BeTrue();
        Directory.EnumerateFileSystemEntries(Path.Combine(stateDirectory.DirectoryPath, "recovery")).Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_UnicodeEncodedDocument_WHEN_Committing_THEN_ShouldPreserveDocumentEncoding()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        await File.WriteAllTextAsync(fixture.DocumentPath, """
            namespace Sample;

            public sealed class Class1
            {
            }
            """, encoding, TestContext.Current.CancellationToken);

        await using var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);
        var preview = await target.PreviewTransactionAsync(new TransactionPreviewRequest(), TestContext.Current.CancellationToken);

        var commit = await target.CommitTransactionAsync(new TransactionCommitRequest
        {
            ExpectedSnapshot = new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = preview.WorkspaceId,
                WorkspaceEpoch = preview.WorkspaceEpoch!.Value,
                TransactionRevision = preview.TransactionRevision,
            },
        }, TestContext.Current.CancellationToken);
        var text = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        var bytes = await File.ReadAllBytesAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);

        commit.Outcome.Should().Be(ToolOutcome.Succeeded);
        bytes.Should().StartWith(encoding.GetPreamble());
        text.Should().Contain("TransactionMarker");
    }

    [Fact]
    public async Task GIVEN_ChangedWorkspaceInputDuringTransaction_WHEN_GettingStatus_THEN_ShouldTransitionToTransactionConflicted()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);
        await File.AppendAllTextAsync(fixture.DocumentPath, Environment.NewLine + "class ExternalChange { }", TestContext.Current.CancellationToken);

        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), TestContext.Current.CancellationToken);

        status.Data!.State.Should().Be(WorkspaceLifecycleState.TransactionConflicted);
        status.Data.Transaction!.CanMutate.Should().BeFalse();
    }

    private static async Task<IWorkspaceRuntime> CreateCoordinatorWithOneStagedRevisionAsync(TestWorkspaceFixture fixture)
    {
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(new TransactionStartRequest(), TestContext.Current.CancellationToken);
        await StageMutationAsync(target);

        return target;
    }

    private static async Task<PluginExecutionResult<MutationData>> StageMutationAsync(
        IWorkspaceRuntime target,
        bool stageEveryDocument = false)
    {
        await using var lease = target.CreateMutationContext(new StageMutationRequest(), TestContext.Current.CancellationToken);
        lease.HasFailure.Should().BeFalse();
        var candidateSolution = lease.Context!.CurrentSolution;
        var documents = stageEveryDocument
            ? candidateSolution.Projects.SelectMany(static project => project.Documents)
            : candidateSolution.Projects.SelectMany(static project => project.Documents).Where(static document => document.Name == "Class1.cs");

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

    private sealed record StageMutationRequest : WorkspaceBoundRequest;
}
