using System.Text;
using System.Text.Json;

using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceCoordinatorTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void GIVEN_WorkspaceCoordinatorContract_WHEN_InspectingTransactionSurface_THEN_ShouldExposeStageFiveServerOperations()
    {
        typeof(IWorkspaceCoordinator).GetMethod("StartTransactionAsync", [typeof(TransactionStartRequest), typeof(CancellationToken)]).Should().NotBeNull();
        typeof(IWorkspaceCoordinator).GetMethod("PreviewTransactionAsync", [typeof(TransactionPreviewRequest), typeof(CancellationToken)]).Should().NotBeNull();
        typeof(IWorkspaceCoordinator).GetMethod("MoveTransactionHistoryAsync", [typeof(TransactionHistoryRequest), typeof(CancellationToken)]).Should().NotBeNull();
        typeof(IWorkspaceCoordinator).GetMethod("CommitTransactionAsync", [typeof(TransactionCommitRequest), typeof(CancellationToken)]).Should().NotBeNull();
        typeof(IWorkspaceCoordinator).GetMethod("RollbackTransactionAsync", [typeof(TransactionRollbackRequest), typeof(CancellationToken)]).Should().NotBeNull();
        typeof(IWorkspaceCoordinator).GetMethod("ListAsync", [typeof(WorkspaceListRequest), typeof(CancellationToken)]).Should().NotBeNull();
    }

    [Fact]
    public async Task GIVEN_UnloadedCoordinator_WHEN_OpeningWorkspace_THEN_ShouldTransitionToReady()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();

        var result = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data.Should().NotBeNull();
        result.Data!.Workspace.Should().NotBeNull();
        result.Data.ProjectCount.Should().Be(1);
        result.Data.DocumentCount.Should().BeGreaterThan(0);

        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

        status.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        status.Data.Workspace!.LoadedPath.Should().Be(fixture.ProjectPath);
        status.WorkspaceEpoch.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_ReadyCoordinator_WHEN_ClosingWorkspace_THEN_ShouldTransitionToUnloaded()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        var result = await target.CloseAsync(new WorkspaceCloseRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.ClosedPath.Should().Be(fixture.ProjectPath);

        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

        status.Outcome.Should().Be(ToolOutcome.Rejected);
        status.Error!.Code.Should().Be("WorkspaceNotOpen");
    }

    [Fact]
    public async Task GIVEN_ChangedWorkspaceInput_WHEN_GettingStatus_THEN_ShouldTransitionToWorkspaceOutOfDate()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await File.AppendAllTextAsync(fixture.DocumentPath, Environment.NewLine + "class Added { }", TestContext.Current.CancellationToken);

        var result = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.State.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
        result.Data.ReloadRequired.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_AddedWorkspaceInput_WHEN_GettingStatus_THEN_ShouldTransitionToWorkspaceOutOfDate()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        var addedDocumentPath = Path.Combine(Path.GetDirectoryName(fixture.DocumentPath)!, "Added.cs");
        await File.WriteAllTextAsync(addedDocumentPath, """
            namespace Sample;

            public sealed class Added
            {
            }
            """, TestContext.Current.CancellationToken);

        var result = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.State.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
        result.Data.ReloadRequired.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_ChangedDirectoryBuildProps_WHEN_GettingStatus_THEN_ShouldTransitionToWorkspaceOutOfDate()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await File.AppendAllTextAsync(fixture.DirectoryBuildPropsPath, Environment.NewLine + "<!-- changed -->", TestContext.Current.CancellationToken);

        var result = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.State.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
        result.Data.ReloadRequired.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_ChangedEditorConfig_WHEN_CreatingQueryContext_THEN_ShouldRejectAsWorkspaceOutOfDate()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await File.AppendAllTextAsync(fixture.EditorConfigPath, Environment.NewLine + "dotnet_diagnostic.CS0168.severity = warning", TestContext.Current.CancellationToken);

        await using var result = await target.CreateQueryContextAsync(new RegisteredTool(), new object(), CancellationToken.None);

        result.ShortCircuitResult.Should().NotBeNull();
        result.ShortCircuitResult!.Error!.Code.Should().Be("WorkspaceOutOfDate");
        result.ShortCircuitResult.RequiredAction.Should().Be(RequiredAction.ReloadWorkspace);
    }

    [Fact]
    public async Task GIVEN_OutOfDateWorkspace_WHEN_Reloading_THEN_ShouldTransitionBackToReady()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await File.AppendAllTextAsync(fixture.DocumentPath, Environment.NewLine + "class Added { }", TestContext.Current.CancellationToken);
        await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

        var result = await target.ReloadAsync(new WorkspaceReloadRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Workspace.Should().NotBeNull();

        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

        status.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        status.Data.ReloadRequired.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_QueryLeaseInFlight_WHEN_GettingWorkspaceStatus_THEN_ShouldSucceed()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        await using var queryLease = await target.CreateQueryContextAsync(new RegisteredTool(), new object(), CancellationToken.None);

        var result = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Error.Should().BeNull();
        result.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
    }

    [Fact]
    public async Task GIVEN_TwoOpenedWorkspaces_WHEN_ListingAndGettingStatus_THEN_ShouldRequireExplicitSelection()
    {
        using var fixtureA = await TestWorkspaceFixture.CreateAsync();
        using var fixtureB = await TestWorkspaceFixture.CreateAsync();
        var target = fixtureA.CreateCoordinator();

        var openA = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Alias = "alpha",
            Path = fixtureA.ProjectPath,
        }, CancellationToken.None);
        var openB = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Alias = "beta",
            Path = fixtureB.ProjectPath,
        }, CancellationToken.None);

        var list = await target.ListAsync(new WorkspaceListRequest(), CancellationToken.None);
        var ambiguousStatus = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);
        var selectedStatus = await target.GetStatusAsync(new WorkspaceStatusRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = openB.Data!.Workspace!.WorkspaceId,
            },
        }, CancellationToken.None);

        list.Outcome.Should().Be(ToolOutcome.Succeeded);
        list.Data!.Workspaces.Should().HaveCount(2);
        list.Data.Workspaces.Select(static workspace => workspace.WorkspaceId).Should().Contain([openA.Data!.Workspace!.WorkspaceId, openB.Data!.Workspace!.WorkspaceId]);
        ambiguousStatus.Outcome.Should().Be(ToolOutcome.Rejected);
        ambiguousStatus.Error!.Code.Should().Be("WorkspaceSelectorRequired");
        selectedStatus.Outcome.Should().Be(ToolOutcome.Succeeded);
        selectedStatus.Data!.Workspace!.WorkspaceId.Should().Be(openB.Data!.Workspace!.WorkspaceId);
        selectedStatus.Data.Workspace.Alias.Should().Be("beta");
    }

    [Fact]
    public async Task GIVEN_TwoOpenedWorkspaces_WHEN_StartingTransactionOnSecondWorkspace_THEN_ShouldRejectUntilOwnerRollsBack()
    {
        using var fixtureA = await TestWorkspaceFixture.CreateAsync();
        using var fixtureB = await TestWorkspaceFixture.CreateAsync();
        var target = fixtureA.CreateCoordinator();

        var openA = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Alias = "alpha",
            Path = fixtureA.ProjectPath,
        }, CancellationToken.None);
        var openB = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Alias = "beta",
            Path = fixtureB.ProjectPath,
        }, CancellationToken.None);

        var startA = await target.StartTransactionAsync(new TransactionStartRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = openA.Data!.Workspace!.WorkspaceId,
            },
        }, CancellationToken.None);
        var startBRejected = await target.StartTransactionAsync(new TransactionStartRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = openB.Data!.Workspace!.WorkspaceId,
            },
        }, CancellationToken.None);
        var rollbackA = await target.RollbackTransactionAsync(new TransactionRollbackRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = openA.Data!.Workspace!.WorkspaceId,
            },
        }, CancellationToken.None);
        var startBAfterRollback = await target.StartTransactionAsync(new TransactionStartRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = openB.Data!.Workspace!.WorkspaceId,
            },
        }, CancellationToken.None);

        startA.Outcome.Should().Be(ToolOutcome.Succeeded);
        startBRejected.Outcome.Should().Be(ToolOutcome.Rejected);
        startBRejected.Error!.Code.Should().Be("TransactionOwnedByWorkspace");
        startBRejected.Error.Message.Should().Contain("alpha");
        rollbackA.Outcome.Should().Be(ToolOutcome.Succeeded);
        startBAfterRollback.Outcome.Should().Be(ToolOutcome.Succeeded);
        startBAfterRollback.WorkspaceId.Should().Be(openB.Data!.Workspace!.WorkspaceId);
    }

    [Fact]
    public async Task GIVEN_NonSdkStyleProject_WHEN_OpeningWorkspace_THEN_ShouldRejectRequest()
    {
        using var fixture = await TestWorkspaceFixture.CreateLegacyProjectAsync();
        var target = fixture.CreateCoordinator();

        var result = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("WorkspaceNotSupported");
    }

    [Fact]
    public async Task GIVEN_UnloadedCoordinator_WHEN_ClosingWorkspace_THEN_ShouldRejectRequest()
    {
        var target = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions());

        var result = await target.CloseAsync(new WorkspaceCloseRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("WorkspaceNotOpen");
    }

    [Fact]
    public async Task GIVEN_OpenedWorkspaceWithoutTransaction_WHEN_CreatingMutationContext_THEN_ShouldRejectWithNoActiveTransaction()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        await using var result = await target.CreateMutationContextAsync(new RegisteredTool(), new object(), CancellationToken.None);

        result.ShortCircuitResult.Should().NotBeNull();
        result.ShortCircuitResult!.Error!.Code.Should().Be("NoActiveTransaction");
        result.ShortCircuitResult.RequiredAction.Should().Be(RequiredAction.StartTransaction);
        result.Context.Should().NotBeNull();
    }

    [Fact]
    public async Task GIVEN_ReadyWorkspace_WHEN_StartingTransaction_THEN_ShouldReportActiveTransactionCapabilities()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        var openResult = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        var result = await target.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

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
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await target.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var tool = CreateStageMutationTool();
        var executor = new ToolExecutor(target);

        var result = await executor.ExecuteAsync(tool, new Dictionary<string, JsonElement>(), CancellationToken.None);
        var payload = DeserializeMutationToolResult(result.StructuredContent!.Value, tool.Metadata.Name);
        var preview = await target.PreviewTransactionAsync(new TransactionPreviewRequest
        {
            IncludeDiff = true,
            Document = new Contracts.Selectors.DocumentSelector
            {
                Path = "Class1.cs",
            },
        }, CancellationToken.None);

        result.IsError.Should().BeFalse();
        payload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        payload.TransactionRevision.Should().Be(1);
        payload.Data!.Operation.Should().Be("test-stage-mutation");
        preview.Data!.Transaction!.Revision.Should().Be(1);
        preview.Data.Documents.Should().ContainSingle();
        preview.Data.Diff.Should().NotBeNull();
        preview.Data.Diff!.Hunks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GIVEN_StagedTransaction_WHEN_MovingHistoryBackwardAndForward_THEN_ShouldUpdateCurrentRevision()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);
        var preview = await target.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        var undo = await target.MoveTransactionHistoryAsync(new TransactionHistoryRequest
        {
            Direction = TransactionHistoryDirection.Undo,
            ExpectedSnapshot = new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = preview.WorkspaceId,
                WorkspaceEpoch = preview.WorkspaceEpoch!.Value,
                TransactionRevision = preview.TransactionRevision,
            },
        }, CancellationToken.None);
        var redo = await target.MoveTransactionHistoryAsync(new TransactionHistoryRequest
        {
            Direction = TransactionHistoryDirection.Redo,
            ExpectedSnapshot = new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = undo.WorkspaceId,
                WorkspaceEpoch = undo.WorkspaceEpoch!.Value,
                TransactionRevision = undo.TransactionRevision,
            },
        }, CancellationToken.None);

        undo.Data!.Transaction!.Revision.Should().Be(0);
        undo.Data.Transaction.CanRedo.Should().BeTrue();
        redo.Data!.Transaction!.Revision.Should().Be(1);
        redo.Data.Transaction.CanUndo.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_FullRevisionHistoryAfterUndo_WHEN_StagingNewMutation_THEN_ShouldTruncateRedoAndFreeCapacity()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            MaxConcurrentQueries = 2,
            MaxTransactionRevisions = 1,
        });
        var open = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await target.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var executor = new ToolExecutor(target);

        var firstTool = CreateStageMutationTool();
        var firstResult = await executor.ExecuteAsync(firstTool, new Dictionary<string, JsonElement>(), CancellationToken.None);
        var firstPayload = DeserializeMutationToolResult(firstResult.StructuredContent!.Value, firstTool.Metadata.Name);
        var undo = await target.MoveTransactionHistoryAsync(new TransactionHistoryRequest
        {
            Direction = TransactionHistoryDirection.Undo,
            ExpectedSnapshot = new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 1,
            },
        }, CancellationToken.None);
        var secondTool = CreateStageMutationTool();
        var secondResult = await executor.ExecuteAsync(secondTool, new Dictionary<string, JsonElement>(), CancellationToken.None);
        var secondPayload = DeserializeMutationToolResult(secondResult.StructuredContent!.Value, secondTool.Metadata.Name);

        firstPayload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        undo.Outcome.Should().Be(ToolOutcome.Succeeded);
        undo.Data!.Transaction!.CanMutate.Should().BeTrue();
        secondPayload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        secondPayload.TransactionRevision.Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_StagedTransaction_WHEN_RollingBack_THEN_ShouldClearTransactionAndReturnReady()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);

        var rollback = await target.RollbackTransactionAsync(new TransactionRollbackRequest(), CancellationToken.None);
        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

        rollback.Outcome.Should().Be(ToolOutcome.Succeeded);
        rollback.Data!.State.Should().Be(TransactionRollbackState.Ready);
        status.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        status.Data.Transaction.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_StagedTransaction_WHEN_Committing_THEN_ShouldPersistDocumentChangesToDisk()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);
        var preview = await target.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        var commit = await target.CommitTransactionAsync(new TransactionCommitRequest
        {
            ExpectedSnapshot = new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = preview.WorkspaceId,
                WorkspaceEpoch = preview.WorkspaceEpoch!.Value,
                TransactionRevision = preview.TransactionRevision,
            },
        }, CancellationToken.None);
        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);
        var text = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);

        commit.Outcome.Should().Be(ToolOutcome.Succeeded);
        commit.Data!.Committed.Should().BeTrue();
        status.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        text.Should().Contain("TransactionMarker");
    }

    [Fact]
    public async Task GIVEN_UnicodeEncodedDocument_WHEN_Committing_THEN_ShouldPreserveDocumentEncoding()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        await File.WriteAllTextAsync(fixture.DocumentPath, """
            namespace Sample;

            public sealed class Class1
            {
            }
            """, encoding, TestContext.Current.CancellationToken);

        var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);
        var preview = await target.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        var commit = await target.CommitTransactionAsync(new TransactionCommitRequest
        {
            ExpectedSnapshot = new Contracts.Selectors.SnapshotPrecondition
            {
                WorkspaceId = preview.WorkspaceId,
                WorkspaceEpoch = preview.WorkspaceEpoch!.Value,
                TransactionRevision = preview.TransactionRevision,
            },
        }, CancellationToken.None);
        var text = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        var bytes = await File.ReadAllBytesAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);

        commit.Outcome.Should().Be(ToolOutcome.Succeeded);
        bytes.Should().StartWith(encoding.GetPreamble());
        text.Should().Contain("TransactionMarker");
    }

    [Fact]
    public async Task GIVEN_AppendOnlyMutation_WHEN_PreviewingDocumentDiffWithZeroContext_THEN_ShouldNotReportUnchangedLinesAsRemovals()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);

        var preview = await target.PreviewTransactionAsync(new TransactionPreviewRequest
        {
            IncludeDiff = true,
            ContextLines = 0,
            Document = new Contracts.Selectors.DocumentSelector
            {
                Path = "Class1.cs",
            },
        }, CancellationToken.None);

        preview.Outcome.Should().Be(ToolOutcome.Succeeded);
        preview.Data!.Diff.Should().NotBeNull();
        preview.Data.Diff!.Hunks.Should().ContainSingle();
        preview.Data.Diff.Hunks[0].Lines.Should().Contain(static line => line.Contains("TransactionMarker", StringComparison.Ordinal));
        preview.Data.Diff.Hunks[0].Lines.Should().NotContain(static line => line.StartsWith("-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_MutationProposalThatChangesCompilationOptions_WHEN_Staging_THEN_ShouldRejectUnsupportedChange()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await target.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var executor = new ToolExecutor(target);

        var tool = CreateCompilationOptionsMutationTool();
        var result = await executor.ExecuteAsync(tool, new Dictionary<string, JsonElement>(), CancellationToken.None);
        var payload = DeserializeMutationToolResult(result.StructuredContent!.Value, tool.Metadata.Name);

        result.IsError.Should().BeTrue();
        payload!.Outcome.Should().Be(ToolOutcome.Rejected);
        payload.Error!.Code.Should().Be("UnsupportedChange");
    }

    [Fact]
    public async Task GIVEN_ChangedWorkspaceInputDuringTransaction_WHEN_GettingStatus_THEN_ShouldTransitionToTransactionConflicted()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = await CreateCoordinatorWithOneStagedRevisionAsync(fixture);
        await File.AppendAllTextAsync(fixture.DocumentPath, Environment.NewLine + "class ExternalChange { }", TestContext.Current.CancellationToken);

        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

        status.Data!.State.Should().Be(WorkspaceLifecycleState.TransactionConflicted);
        status.Data.Transaction!.CanMutate.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_GettingWorkspaceStatus_THEN_ShouldThrowOperationCanceledException()
    {
        var target = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var action = async () => await target.GetStatusAsync(new WorkspaceStatusRequest(), cancellationTokenSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_CreatingQueryContext_THEN_ShouldThrowOperationCanceledException()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var action = async () => await target.CreateQueryContextAsync(new RegisteredTool(), new object(), cancellationTokenSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_OpeningWorkspace_THEN_ShouldThrowOperationCanceledException()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var action = async () => await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, cancellationTokenSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_QueryContext_WHEN_Acquired_THEN_ShouldExposeConfiguredResponseByteLimit()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 2048,
        });
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        await using var result = await target.CreateQueryContextAsync(new RegisteredTool(), new object(), CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Context!.MaxResponseBytes.Should().Be(2048);
    }

    [Fact]
    public async Task GIVEN_MalformedProject_WHEN_OpeningWorkspace_THEN_ShouldReturnStructuredLoadDiagnostics()
    {
        using var fixture = await TestWorkspaceFixture.CreateMalformedProjectAsync();
        var target = fixture.CreateCoordinator();

        var result = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("WorkspaceLoadFailed");
        result.Diagnostics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GIVEN_MalformedProjectAfterExternalChange_WHEN_ReloadingWorkspace_THEN_ShouldReturnStructuredLoadDiagnostics()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await File.WriteAllTextAsync(fixture.ProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
            """, TestContext.Current.CancellationToken);
        await target.GetStatusAsync(new WorkspaceStatusRequest(), CancellationToken.None);

        var result = await target.ReloadAsync(new WorkspaceReloadRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("WorkspaceLoadFailed");
        result.Diagnostics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GIVEN_UnresolvedRecoveryState_WHEN_OpeningWorkspace_THEN_ShouldRejectRequest()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var stateDirectory = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-recovery-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(stateDirectory);
        CommitRecoveryStore.WriteStatus(stateDirectory, new RecoveryStatus
        {
            CommitId = "commit-id",
            SolutionPath = fixture.ProjectPath,
            State = RecoveryState.RecoveryIncomplete,
            Message = "Message",
        });
        var target = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            StateDirectory = stateDirectory,
        });

        var result = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.RequiredAction.Should().Be(RequiredAction.ResolveRecovery);
    }

    private static RegisteredTool CreateStageMutationTool()
    {
        var registry = new PluginRegistry(new PluginMetadata
        {
            PluginId = "workspace.test",
            DisplayName = "Workspace Test",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        });
        registry.RegisterMutationTool(
            new ToolRegistrationMetadata
            {
                Name = "test-stage-mutation",
                Title = "Test Stage Mutation",
                Description = "Stages a predictable document edit.",
            },
            new StageMutationHandler());

        return registry.RegisteredTools.Single();
    }

    private static RegisteredTool CreateCompilationOptionsMutationTool()
    {
        var registry = new PluginRegistry(new PluginMetadata
        {
            PluginId = "workspace.test",
            DisplayName = "Workspace Test",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        });
        registry.RegisterMutationTool(
            new ToolRegistrationMetadata
            {
                Name = "test-stage-compilation-options-mutation",
                Title = "Test Stage Compilation Options Mutation",
                Description = "Stages an unsupported compilation options change.",
            },
            new CompilationOptionsMutationHandler());

        return registry.RegisteredTools.Single();
    }

    private static ToolResult<MutationData> DeserializeMutationToolResult(JsonElement payload, string toolName)
    {
        if (payload.TryGetProperty("outcome", out _))
        {
            return JsonSerializer.Deserialize<ToolResult<MutationData>>(payload.GetRawText(), _serializerOptions)!;
        }

        if (!payload.GetProperty("ok").GetBoolean())
        {
            return ToolResult<MutationData>.Rejected(
                JsonSerializer.Deserialize<ToolError>(payload.GetProperty("error").GetRawText(), _serializerOptions)!,
                payload.TryGetProperty("next", out var nextElement) && nextElement.ValueKind != JsonValueKind.Null
                    ? JsonSerializer.Deserialize<RequiredAction>(nextElement.GetRawText(), _serializerOptions)
                    : null);
        }

        var transaction = payload.TryGetProperty("transaction", out var transactionElement)
            ? new TransactionInfo
            {
                Revision = transactionElement.GetProperty("revision").GetInt32(),
            }
            : null;

        return ToolResult<MutationData>.Succeeded(
            new MutationData
            {
                Operation = toolName,
                Summary = payload.TryGetProperty("summary", out var summaryElement) && summaryElement.ValueKind == JsonValueKind.String
                    ? summaryElement.GetString() ?? string.Empty
                    : string.Empty,
                Transaction = transaction,
            },
            transactionRevision: transaction?.Revision);
    }

    private static async Task<IWorkspaceCoordinator> CreateCoordinatorWithOneStagedRevisionAsync(TestWorkspaceFixture fixture)
    {
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await target.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var executor = new ToolExecutor(target);

        await executor.ExecuteAsync(CreateStageMutationTool(), new Dictionary<string, JsonElement>(), CancellationToken.None);

        return target;
    }

    private sealed record StageMutationRequest;

    private sealed class StageMutationHandler : IMutationToolHandler<StageMutationRequest, MutationProposal>
    {
        public async ValueTask<PluginExecutionResult<MutationProposal>> ExecuteAsync(StageMutationRequest request, IMutationContext context, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();

            var document = context.CurrentSolution.Projects.SelectMany(static project => project.Documents).Single(static document => document.Name == "Class1.cs");
            var sourceText = await document.GetTextAsync(cancellationToken);
            var candidateSolution = context.CurrentSolution.WithDocumentText(
                document.Id,
                sourceText.WithChanges(
                [
                    new Microsoft.CodeAnalysis.Text.TextChange(
                        new Microsoft.CodeAnalysis.Text.TextSpan(sourceText.Length, 0),
                        Environment.NewLine + "public sealed class TransactionMarker { }" + Environment.NewLine),
                ]));

            return PluginExecutionResult<MutationProposal>.Success(new MutationProposal
            {
                CandidateSolution = candidateSolution,
                Summary = "Stage transaction marker.",
            });
        }
    }

    private sealed class CompilationOptionsMutationHandler : IMutationToolHandler<StageMutationRequest, MutationProposal>
    {
        public ValueTask<PluginExecutionResult<MutationProposal>> ExecuteAsync(StageMutationRequest request, IMutationContext context, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();

            var project = context.CurrentSolution.Projects.Single();
            var compilationOptions = (CSharpCompilationOptions?)project.CompilationOptions;
            var updatedOptions = compilationOptions!.OptimizationLevel == Microsoft.CodeAnalysis.OptimizationLevel.Debug
                ? compilationOptions.WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Release)
                : compilationOptions.WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Debug);
            var candidateSolution = context.CurrentSolution.WithProjectCompilationOptions(project.Id, updatedOptions);

            return ValueTask.FromResult(PluginExecutionResult<MutationProposal>.Success(new MutationProposal
            {
                CandidateSolution = candidateSolution,
                Summary = "Change compilation options.",
            }));
        }
    }
}
