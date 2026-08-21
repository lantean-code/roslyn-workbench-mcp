using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.State;

public sealed class WorkspaceSessionStoreTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceSnapshotLifecycleObserver> _lifecycleObserver;
    private readonly Mock<IWorkspaceQueryCache> _queryCache;

    public WorkspaceSessionStoreTests()
    {
        _workspace = new AdhocWorkspace();
        _lifecycleObserver = new Mock<IWorkspaceSnapshotLifecycleObserver>();
        _queryCache = new Mock<IWorkspaceQueryCache>();
    }

    [Fact]
    public void GIVEN_NewStore_WHEN_ReadingSnapshot_THEN_ShouldReturnEmptySnapshotWithoutOwner()
    {
        var target = CreateStore();

        var result = target.ReadSnapshot();

        result.Workspaces.Should().BeEmpty();
        result.TransactionOwnerWorkspaceId.Should().BeNull();
    }

    [Fact]
    public void GIVEN_IndependentStores_WHEN_AllocatingWorkspaceIds_THEN_ShouldReturnGloballyUniqueIds()
    {
        var firstTarget = CreateStore();
        var secondTarget = CreateStore();

        var first = firstTarget.AllocateWorkspaceId();
        var second = secondTarget.AllocateWorkspaceId();

        first.Should().NotBe(Guid.Empty);
        second.Should().NotBe(Guid.Empty);
        first.Should().NotBe(second);
    }

    [Fact]
    public void GIVEN_NewStore_WHEN_AllocatingWorkspaceEpochs_THEN_ShouldReturnMonotonicallyIncreasingEpochs()
    {
        var target = CreateStore();

        var first = target.AllocateWorkspaceEpoch();
        var second = target.AllocateWorkspaceEpoch();

        first.Should().Be(1);
        second.Should().Be(2);
    }

    [Fact]
    public void GIVEN_NewStore_WHEN_AllocatingSnapshotAndTransactionIds_THEN_ShouldReturnUniqueSnapshotsAndMonotonicTransactions()
    {
        var target = CreateStore();

        var firstSnapshot = target.AllocateWorkspaceSnapshotId();
        var secondSnapshot = target.AllocateWorkspaceSnapshotId();
        var firstTransaction = target.AllocateWorkspaceTransactionId();
        var secondTransaction = target.AllocateWorkspaceTransactionId();

        firstSnapshot.Value.Should().NotBe(Guid.Empty);
        secondSnapshot.Value.Should().NotBe(Guid.Empty);
        firstSnapshot.Should().NotBe(secondSnapshot);
        firstTransaction.Value.Should().Be(1);
        secondTransaction.Value.Should().Be(2);
    }

    [Fact]
    public void GIVEN_SnapshotAndTransactionIdentityTypes_WHEN_UsingInvalidValues_THEN_ShouldReserveDefaultAndRejectNonPositiveValues()
    {
        default(WorkspaceSnapshotId).Value.Should().Be(Guid.Empty);
        default(WorkspaceTransactionId).Value.Should().Be(0);

        var emptySnapshotAction = () => new WorkspaceSnapshotId(Guid.Empty);
        var zeroTransactionAction = () => new WorkspaceTransactionId(0);
        var negativeTransactionAction = () => new WorkspaceTransactionId(-1);

        emptySnapshotAction.Should().Throw<ArgumentOutOfRangeException>();
        zeroTransactionAction.Should().Throw<ArgumentOutOfRangeException>();
        negativeTransactionAction.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GIVEN_ConcurrentCallers_WHEN_AllocatingSnapshotAndTransactionIds_THEN_ShouldReturnUniqueIds()
    {
        const int allocationCount = 100;
        var target = CreateStore();
        var snapshotIds = new Guid[allocationCount];
        var transactionIds = new long[allocationCount];

        Parallel.For(0, allocationCount, index =>
        {
            snapshotIds[index] = target.AllocateWorkspaceSnapshotId().Value;
            transactionIds[index] = target.AllocateWorkspaceTransactionId().Value;
        });

        snapshotIds.Should().OnlyHaveUniqueItems();
        transactionIds.Should().OnlyHaveUniqueItems();
        transactionIds.Should().BeEquivalentTo(Enumerable.Range(1, allocationCount).Select(value => (long)value));
    }

    [Fact]
    public void GIVEN_ValidationFailure_WHEN_AddingWorkspace_THEN_ShouldReturnErrorWithoutMutatingSnapshot()
    {
        var target = CreateStore();
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias");
        var error = new WorkspaceOperationError
        {
            Code = "Code",
            Message = "Message",
        };

        var validate = new Mock<Func<WorkspaceHostSnapshot, WorkspaceOperationError?>>();
        validate.Setup(item => item(It.IsAny<WorkspaceHostSnapshot>())).Returns(error);

        var result = target.TryAddWorkspace(session, validate.Object);

        result.Should().BeSameAs(error);
        target.ReadSnapshot().Workspaces.Should().BeEmpty();
        validate.Verify(item => item(It.IsAny<WorkspaceHostSnapshot>()), Times.Once);
    }

    [Fact]
    public void GIVEN_ValidSession_WHEN_AddingAndReadingWorkspace_THEN_ShouldReturnAddedSession()
    {
        var target = CreateStore();
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias");
        var validate = new Mock<Func<WorkspaceHostSnapshot, WorkspaceOperationError?>>();
        validate.Setup(item => item(It.IsAny<WorkspaceHostSnapshot>())).Returns((WorkspaceOperationError?)null);

        var error = target.TryAddWorkspace(session, validate.Object);
        var result = target.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        error.Should().BeNull();
        result.Should().BeSameAs(session);
        target.ReadSession(Guid.Parse("44444444-4444-4444-4444-444444444444")).Should().BeNull();
    }

    [Fact]
    public void GIVEN_UnknownWorkspace_WHEN_Removing_THEN_ShouldReturnNullWithoutChangingSnapshot()
    {
        var target = CreateStoreWithSession(CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias"));
        var snapshot = target.ReadSnapshot();

        var result = target.RemoveWorkspace(Guid.Parse("44444444-4444-4444-4444-444444444444"));

        result.Should().BeNull();
        target.ReadSnapshot().Should().BeSameAs(snapshot);
        _queryCache.Verify(item => item.InvalidateWorkspace(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public void GIVEN_OwnerWorkspace_WHEN_Removing_THEN_ShouldRemoveSessionAndClearOwner()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias");
        var target = CreateStoreWithSession(session);
        target.TryStartTransaction(session);

        var result = target.RemoveWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        result.Should().BeSameAs(session);
        target.ReadSnapshot().Workspaces.Should().BeEmpty();
        target.ReadSnapshot().TransactionOwnerWorkspaceId.Should().BeNull();
        _queryCache.Verify(item => item.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111")), Times.Once);
        _lifecycleObserver.Verify(
            item => item.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1),
            Times.Once);
    }

    [Fact]
    public void GIVEN_NonOwnerWorkspace_WHEN_Removing_THEN_ShouldPreserveDifferentOwner()
    {
        var firstSession = CreateSession(Guid.Parse("55555555-5555-5555-5555-555555555555"), "FirstAlias");
        var secondSession = CreateSession(Guid.Parse("66666666-6666-6666-6666-666666666666"), "SecondAlias");
        var target = CreateStoreWithSession(firstSession);
        AddSession(target, secondSession);
        target.TryStartTransaction(firstSession);

        var result = target.RemoveWorkspace(Guid.Parse("66666666-6666-6666-6666-666666666666"));

        result.Should().BeSameAs(secondSession);
        target.ReadSnapshot().TransactionOwnerWorkspaceId.Should().Be(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        target.ReadSession(Guid.Parse("55555555-5555-5555-5555-555555555555")).Should().BeSameAs(firstSession);
        _queryCache.Verify(item => item.InvalidateWorkspace(Guid.Parse("66666666-6666-6666-6666-666666666666")), Times.Once);
        _lifecycleObserver.Verify(
            item => item.InvalidateWorkspace(Guid.Parse("66666666-6666-6666-6666-666666666666"), 1),
            Times.Once);
    }

    [Fact]
    public void GIVEN_ReplacementSession_WHEN_Replacing_THEN_ShouldUpdateOnlyMatchingWorkspace()
    {
        var firstSession = CreateSession(Guid.Parse("55555555-5555-5555-5555-555555555555"), "FirstAlias");
        var secondSession = CreateSession(Guid.Parse("66666666-6666-6666-6666-666666666666"), "SecondAlias");
        var target = CreateStoreWithSession(firstSession);
        AddSession(target, secondSession);
        var replacement = CreateSession(Guid.Parse("55555555-5555-5555-5555-555555555555"), "ReplacementAlias");

        target.ReplaceSession(replacement);

        target.ReadSession(Guid.Parse("55555555-5555-5555-5555-555555555555")).Should().BeSameAs(replacement);
        target.ReadSession(Guid.Parse("66666666-6666-6666-6666-666666666666")).Should().BeSameAs(secondSession);
        _queryCache.Verify(item => item.InvalidateWorkspace(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public void GIVEN_ReplacementAndNoOwner_WHEN_StartingTransaction_THEN_ShouldUpdateSessionAndOwner()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias");
        var target = CreateStoreWithSession(session);
        var replacement = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "ReplacementAlias");

        var result = target.TryStartTransaction(replacement);

        result.IsAdmitted.Should().BeTrue();
        result.ExistingOwnerWorkspaceId.Should().BeNull();
        target.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111")).Should().BeSameAs(replacement);
        target.ReadSnapshot().TransactionOwnerWorkspaceId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        _queryCache.Verify(item => item.InvalidateWorkspace(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_TwoWorkspacesWithoutOwner_WHEN_StartingTransactionsConcurrently_THEN_ShouldAdmitExactlyOne()
    {
        var firstSession = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "FirstAlias");
        var secondSession = CreateSession(Guid.Parse("22222222-2222-2222-2222-222222222222"), "SecondAlias");
        var target = CreateStoreWithSession(firstSession);
        AddSession(target, secondSession);
        var firstTransaction = CreateTransaction();
        var secondTransaction = CreateTransaction();
        var firstReplacement = firstSession with
        {
            State = WorkspaceLifecycleState.TransactionActive,
            Transaction = firstTransaction,
            CurrentSolution = firstTransaction.CurrentSolution,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                firstSession.Workspace,
                firstSession.CommittedSnapshotId,
                firstTransaction),
        };

        var secondReplacement = secondSession with
        {
            State = WorkspaceLifecycleState.TransactionActive,
            Transaction = secondTransaction,
            CurrentSolution = secondTransaction.CurrentSolution,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                secondSession.Workspace,
                secondSession.CommittedSnapshotId,
                secondTransaction),
        };

        using var startGate = new Barrier(2);

        var firstTask = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            return target.TryStartTransaction(firstReplacement);
        });

        var secondTask = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            return target.TryStartTransaction(secondReplacement);
        });

        var results = await Task.WhenAll(firstTask, secondTask);
        var admittedResult = results.Single(result => result.IsAdmitted);
        var rejectedResult = results.Single(result => !result.IsAdmitted);
        var snapshot = target.ReadSnapshot();

        rejectedResult.ExistingOwnerWorkspaceId.Should().Be(snapshot.TransactionOwnerWorkspaceId);
        admittedResult.ExistingOwnerWorkspaceId.Should().BeNull();
        snapshot.Workspaces.Values.Count(session => session.Transaction is not null).Should().Be(1);
    }

    [Fact]
    public void GIVEN_DifferentTransactionOwner_WHEN_CompletingTransaction_THEN_ShouldPreserveOwnerAndSessions()
    {
        var firstTransaction = CreateTransaction();
        var firstSession = CreateSession(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "FirstAlias",
            transaction: firstTransaction);

        var secondSession = CreateSession(Guid.Parse("22222222-2222-2222-2222-222222222222"), "SecondAlias");
        var target = CreateStoreWithSession(firstSession);
        AddSession(target, secondSession);
        var secondReplacement = secondSession with { State = WorkspaceLifecycleState.Ready };

        target.TryStartTransaction(firstSession);

        var result = target.TryCompleteTransaction(secondReplacement);
        var expectedFailure = new TransactionCompletionFailure(
            secondSession.Workspace.WorkspaceId,
            firstSession.Workspace.WorkspaceId);

        result.IsCompleted.Should().BeFalse();
        result.Failure.Should().Be(expectedFailure);
        target.ReadSnapshot().TransactionOwnerWorkspaceId.Should().Be(firstSession.Workspace.WorkspaceId);
        target.ReadSession(secondSession.Workspace.WorkspaceId).Should().BeSameAs(secondSession);
    }

    [Fact]
    public void GIVEN_ReplacementWithNewSolution_WHEN_Replacing_THEN_ShouldInvalidateWorkspaceCache()
    {
        using var workspace = new AdhocWorkspace();
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", workspace.CurrentSolution);
        var target = CreateStoreWithSession(session);
        var changedSolution = workspace.CurrentSolution.AddProject("Project", "Project", LanguageNames.CSharp).Solution;
        var replacement = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", changedSolution);

        target.ReplaceSession(replacement);

        _queryCache.Verify(item => item.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111")), Times.Once);
    }

    [Theory]
    [InlineData(WorkspaceLifecycleState.WorkspaceOutOfDate)]
    [InlineData(WorkspaceLifecycleState.TransactionConflicted)]
    public void GIVEN_UnavailableReplacement_WHEN_Replacing_THEN_ShouldInvalidateWorkspaceCache(
        WorkspaceLifecycleState state)
    {
        using var workspace = new AdhocWorkspace();
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", workspace.CurrentSolution);
        var target = CreateStoreWithSession(session);
        var replacement = session with
        {
            State = state,
        };

        target.ReplaceSession(replacement);

        _queryCache.Verify(item => item.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111")), Times.Once);
    }

    [Fact]
    public void GIVEN_ReadySession_WHEN_BecomingOutOfDate_THEN_ShouldInvalidateCurrentSnapshot()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias");
        var target = CreateStoreWithSession(session);
        var replacement = session with
        {
            State = WorkspaceLifecycleState.WorkspaceOutOfDate,
        };

        target.ReplaceSession(replacement);

        _lifecycleObserver.Verify(
            item => item.InvalidateSnapshots(It.Is<IReadOnlyList<WorkspaceSnapshotIdentity>>(snapshots =>
                snapshots.SequenceEqual(new[] { session.CurrentSnapshotIdentity }))),
            Times.Once);
    }

    [Fact]
    public void GIVEN_ActiveTransaction_WHEN_BecomingConflicted_THEN_ShouldInvalidateTransaction()
    {
        var transaction = CreateTransaction();
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", transaction: transaction);
        var target = CreateStoreWithSession(session);
        var replacement = session with
        {
            State = WorkspaceLifecycleState.TransactionConflicted,
        };

        target.ReplaceSession(replacement);

        _lifecycleObserver.Verify(
            item => item.InvalidateTransaction(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                1,
                transaction.TransactionId),
            Times.Once);
    }

    [Fact]
    public void GIVEN_PreviousSnapshot_WHEN_StoreChanges_THEN_ShouldRemainUnchanged()
    {
        var firstSession = CreateSession(Guid.Parse("55555555-5555-5555-5555-555555555555"), "FirstAlias");
        var target = CreateStoreWithSession(firstSession);
        var previousSnapshot = target.ReadSnapshot();
        var secondSession = CreateSession(Guid.Parse("66666666-6666-6666-6666-666666666666"), "SecondAlias");

        AddSession(target, secondSession);

        previousSnapshot.Workspaces.Should().ContainSingle();
        previousSnapshot.Workspaces.Should().ContainKey(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        target.ReadSnapshot().Workspaces.Should().HaveCount(2);
    }

    [Fact]
    public void GIVEN_CommittedSession_WHEN_StartingTransaction_THEN_ShouldInvalidatePreviousCommittedSnapshot()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias");
        var target = CreateStoreWithSession(session);
        var transaction = CreateTransaction() with
        {
            Revisions = [],
        };

        var replacement = session with
        {
            State = WorkspaceLifecycleState.TransactionActive,
            Transaction = transaction,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                session.Workspace,
                session.CommittedSnapshotId,
                transaction),
        };

        target.TryStartTransaction(replacement);

        _lifecycleObserver.Verify(
            item => item.InvalidateSnapshots(It.Is<IReadOnlyList<WorkspaceSnapshotIdentity>>(snapshots =>
                snapshots.SequenceEqual(new[] { session.CurrentSnapshotIdentity }))),
            Times.Once);

        _lifecycleObserver.Verify(
            item => item.InvalidateTransaction(
                It.IsAny<Guid>(),
                It.IsAny<long>(),
                It.IsAny<WorkspaceTransactionId>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_UnchangedSolution_WHEN_StartingTransaction_THEN_ShouldRetainWorkspaceQueryCache()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias");
        var target = CreateStoreWithSession(session);
        var transaction = CreateTransaction() with
        {
            Revisions = [],
        };

        var replacement = session with
        {
            State = WorkspaceLifecycleState.TransactionActive,
            Transaction = transaction,
            CurrentSolution = transaction.CurrentSolution,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                session.Workspace,
                session.CommittedSnapshotId,
                transaction),
        };

        target.TryStartTransaction(replacement);

        _queryCache.Verify(
            item => item.InvalidateWorkspace(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_ActiveTransaction_WHEN_CommittingOrRollingBack_THEN_ShouldInvalidateTransaction()
    {
        var transaction = CreateTransaction();
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", transaction: transaction);
        var target = CreateStoreWithSession(session);
        var committedSnapshotId = WorkspaceSnapshotTestFactory.CreateId(2);
        var replacement = session with
        {
            State = WorkspaceLifecycleState.Ready,
            Transaction = null,
            CommittedSnapshotId = committedSnapshotId,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                session.Workspace,
                committedSnapshotId,
                transaction: null),
        };

        target.TryStartTransaction(session);
        var result = target.TryCompleteTransaction(replacement);

        result.IsCompleted.Should().BeTrue();
        result.Failure.Should().BeNull();
        _lifecycleObserver.Verify(
            item => item.InvalidateTransaction(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                1,
                transaction.TransactionId),
            Times.Once);
    }

    [Fact]
    public void GIVEN_UnchangedSolution_WHEN_CommittingTransaction_THEN_ShouldRetainWorkspaceQueryCache()
    {
        var changedSolution = _workspace.CurrentSolution
            .AddProject("Project", "Project", LanguageNames.CSharp)
            .Solution;

        var transaction = CreateTransaction(
            currentRevision: 1,
            firstRevisionSolution: changedSolution);

        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", transaction: transaction);
        var target = CreateStoreWithSession(session);
        var committedSnapshotId = WorkspaceSnapshotTestFactory.CreateId(2);
        var replacement = session with
        {
            State = WorkspaceLifecycleState.Ready,
            Transaction = null,
            CommittedSnapshotId = committedSnapshotId,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                session.Workspace,
                committedSnapshotId,
                transaction: null),
        };

        target.TryStartTransaction(session);
        target.TryCompleteTransaction(replacement);

        _queryCache.Verify(
            item => item.InvalidateWorkspace(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_DifferentSolution_WHEN_StagingTransactionRevision_THEN_ShouldInvalidateWorkspaceQueryCache()
    {
        var transaction = CreateTransaction() with
        {
            Revisions = [],
        };

        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", transaction: transaction);
        var target = CreateStoreWithSession(session);
        var changedSolution = _workspace.CurrentSolution
            .AddProject("Project", "Project", LanguageNames.CSharp)
            .Solution;

        var appendResult = transaction.Append(new WorkspaceTransactionRevision
        {
            SnapshotId = WorkspaceSnapshotTestFactory.CreateId(2),
            Solution = changedSolution,
            Changes = new ChangeSummary(),
            Operation = "Operation",
            Summary = "Summary",
            Preview = new MutationPreview(),
        });

        target.ReplaceSessionAfterStaging(
            session with
            {
                Transaction = appendResult.Transaction,
                CurrentSolution = appendResult.Transaction.CurrentSolution,
                CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                    session.Workspace,
                    session.CommittedSnapshotId,
                    appendResult.Transaction),
            },
            appendResult.DiscardedSnapshotIds);

        _queryCache.Verify(
            item => item.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Times.Once);
    }

    [Theory]
    [InlineData(TransactionHistoryDirection.Undo, 2)]
    [InlineData(TransactionHistoryDirection.Redo, 1)]
    public void GIVEN_DifferentReachableSolution_WHEN_MovingTransactionHistory_THEN_ShouldInvalidateWorkspaceQueryCache(
        TransactionHistoryDirection direction,
        int currentRevision)
    {
        var firstRevisionSolution = _workspace.CurrentSolution
            .AddProject("FirstProject", "FirstProject", LanguageNames.CSharp)
            .Solution;

        var secondRevisionSolution = firstRevisionSolution
            .AddProject("SecondProject", "SecondProject", LanguageNames.CSharp)
            .Solution;

        var transaction = CreateTransaction(
            currentRevision,
            firstRevisionSolution: firstRevisionSolution,
            secondRevisionSolution: secondRevisionSolution);

        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", transaction: transaction);
        var target = CreateStoreWithSession(session);
        var movedTransaction = transaction.MoveHistory(direction);

        target.ReplaceSession(session with
        {
            Transaction = movedTransaction,
            CurrentSolution = movedTransaction!.CurrentSolution,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                session.Workspace,
                session.CommittedSnapshotId,
                movedTransaction),
        });

        _queryCache.Verify(
            item => item.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Times.Once);
    }

    [Fact]
    public void GIVEN_DifferentSolution_WHEN_RollingBackTransaction_THEN_ShouldInvalidateWorkspaceQueryCache()
    {
        var baselineSolution = _workspace.CurrentSolution;
        var changedSolution = baselineSolution
            .AddProject("Project", "Project", LanguageNames.CSharp)
            .Solution;

        var transaction = CreateTransaction(
            currentRevision: 1,
            baselineSolution: baselineSolution,
            firstRevisionSolution: changedSolution);

        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", transaction: transaction);
        var target = CreateStoreWithSession(session);
        var replacement = session with
        {
            State = WorkspaceLifecycleState.Ready,
            Transaction = null,
            CurrentSolution = baselineSolution,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                session.Workspace,
                session.CommittedSnapshotId,
                transaction: null),
        };

        target.TryStartTransaction(session);
        target.TryCompleteTransaction(replacement);

        _queryCache.Verify(
            item => item.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Times.Once);
    }

    [Fact]
    public void GIVEN_ReachableTransactionHistory_WHEN_MovingCurrentRevision_THEN_ShouldNotInvalidateReferences()
    {
        var transaction = CreateTransaction(currentRevision: 2);
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", transaction: transaction);
        var target = CreateStoreWithSession(session);
        var movedTransaction = transaction.MoveHistory(TransactionHistoryDirection.Undo);
        var replacement = session with
        {
            Transaction = movedTransaction,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                session.Workspace,
                session.CommittedSnapshotId,
                movedTransaction),
        };

        target.ReplaceSession(replacement);

        _lifecycleObserver.Verify(
            item => item.InvalidateSnapshots(It.IsAny<IReadOnlyList<WorkspaceSnapshotIdentity>>()),
            Times.Never);

        _lifecycleObserver.Verify(
            item => item.InvalidateTransaction(
                It.IsAny<Guid>(),
                It.IsAny<long>(),
                It.IsAny<WorkspaceTransactionId>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_DiscardedRedoBranch_WHEN_ReplacingAfterStaging_THEN_ShouldInvalidateDiscardedSnapshots()
    {
        var transaction = CreateTransaction(currentRevision: 1);
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", transaction: transaction);
        var target = CreateStoreWithSession(session);
        var replacementTransaction = transaction with
        {
            Revisions =
            [
                transaction.Revisions[0],
                new WorkspaceTransactionRevision
                {
                    SnapshotId = WorkspaceSnapshotTestFactory.CreateId(4),
                    Solution = _workspace.CurrentSolution,
                    Changes = new ChangeSummary(),
                    Operation = "Operation",
                    Summary = "Summary",
                    Preview = new MutationPreview(),
                },
            ],
            CurrentRevision = 2,
        };

        target.ReplaceSessionAfterStaging(
            session with
            {
                Transaction = replacementTransaction,
                CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                    session.Workspace,
                    session.CommittedSnapshotId,
                    replacementTransaction),
            },
            [WorkspaceSnapshotTestFactory.CreateId(3)]);

        var expectedIdentity = new WorkspaceSnapshotIdentity(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            1,
            WorkspaceSnapshotTestFactory.CreateId(3),
            transaction.TransactionId);

        _lifecycleObserver.Verify(
            item => item.InvalidateSnapshots(It.Is<IReadOnlyList<WorkspaceSnapshotIdentity>>(snapshots =>
                snapshots.SequenceEqual(new[] { expectedIdentity }))),
            Times.Once);
    }

    [Fact]
    public void GIVEN_NewWorkspaceEpoch_WHEN_ReplacingSession_THEN_ShouldInvalidateSupersededInstance()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias");
        var target = CreateStoreWithSession(session);
        var replacement = CreateSession(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Alias",
            workspaceEpoch: 2,
            committedSnapshotId: 2);

        target.ReplaceSession(replacement);

        _lifecycleObserver.Verify(
            item => item.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1),
            Times.Once);
    }

    [Fact]
    public void GIVEN_NewWorkspaceEpoch_WHEN_ReplacingSession_THEN_ShouldInvalidateWorkspaceQueryCache()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias");
        var target = CreateStoreWithSession(session);
        var replacement = CreateSession(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Alias",
            workspaceEpoch: 2,
            committedSnapshotId: 2);

        target.ReplaceSession(replacement);

        _queryCache.Verify(
            item => item.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Times.Once);
    }

    [Fact]
    public void GIVEN_OpenWorkspacesAndTransactionOwner_WHEN_Draining_THEN_ShouldReturnSessionsAndResetStore()
    {
        var firstSession = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "FirstAlias");
        var secondSession = CreateSession(Guid.Parse("22222222-2222-2222-2222-222222222222"), "SecondAlias");
        var target = CreateStoreWithSession(firstSession);
        AddSession(target, secondSession);
        target.TryStartTransaction(firstSession);

        var result = target.DrainWorkspaces();

        result.Should().Equal(firstSession, secondSession);
        target.ReadSnapshot().Workspaces.Should().BeEmpty();
        target.ReadSnapshot().TransactionOwnerWorkspaceId.Should().BeNull();
    }

    [Fact]
    public void GIVEN_StoreAlreadyDrained_WHEN_DrainingAgain_THEN_ShouldReturnEmptyCollection()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias");
        var target = CreateStoreWithSession(session);
        target.DrainWorkspaces();

        var result = target.DrainWorkspaces();

        result.Should().BeEmpty();
    }

    private WorkspaceSessionStore CreateStoreWithSession(WorkspaceSessionSnapshot session)
    {
        var target = CreateStore();
        AddSession(target, session);
        return target;
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private WorkspaceSessionStore CreateStore()
    {
        return new WorkspaceSessionStore(
            _queryCache.Object,
            [_lifecycleObserver.Object]);
    }

    private static void AddSession(WorkspaceSessionStore target, WorkspaceSessionSnapshot session)
    {
        var validate = new Mock<Func<WorkspaceHostSnapshot, WorkspaceOperationError?>>();
        validate.Setup(item => item(It.IsAny<WorkspaceHostSnapshot>())).Returns((WorkspaceOperationError?)null);
        target.TryAddWorkspace(session, validate.Object).Should().BeNull();
    }

    private WorkspaceSessionSnapshot CreateSession(
        Guid workspaceId,
        string alias,
        Solution? solution = null,
        long workspaceEpoch = 1,
        long committedSnapshotId = 1,
        WorkspaceTransaction? transaction = null)
    {
        var snapshotId = WorkspaceSnapshotTestFactory.CreateId(committedSnapshotId);
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = workspaceId,
            Alias = alias,
            WorkspaceEpoch = workspaceEpoch,
            LoadedPath = "LoadedPath",
        };

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = snapshotId,
            State = transaction is null
                ? WorkspaceLifecycleState.Ready
                : WorkspaceLifecycleState.TransactionActive,
            Workspace = workspaceIdentity,
            LoadedWorkspace = null!,
            CurrentSolution = transaction?.CurrentSolution ?? solution ?? _workspace.CurrentSolution,
            Transaction = transaction,
            InputManifest = null!,
            OperationGate = null!,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                workspaceIdentity,
                snapshotId,
                transaction),
        };
    }

    private WorkspaceTransaction CreateTransaction(
        int currentRevision = 0,
        Solution? baselineSolution = null,
        Solution? firstRevisionSolution = null,
        Solution? secondRevisionSolution = null)
    {
        baselineSolution ??= _workspace.CurrentSolution;
        firstRevisionSolution ??= baselineSolution;
        secondRevisionSolution ??= firstRevisionSolution;

        return new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = baselineSolution,
            Revisions =
            [
                new WorkspaceTransactionRevision
                {
                    SnapshotId = WorkspaceSnapshotTestFactory.CreateId(2),
                    Solution = firstRevisionSolution,
                    Changes = new ChangeSummary(),
                    Operation = "Operation",
                    Summary = "Summary",
                    Preview = new MutationPreview(),
                },
                new WorkspaceTransactionRevision
                {
                    SnapshotId = WorkspaceSnapshotTestFactory.CreateId(3),
                    Solution = secondRevisionSolution,
                    Changes = new ChangeSummary(),
                    Operation = "Operation",
                    Summary = "Summary",
                    Preview = new MutationPreview(),
                },
            ],
            CurrentRevision = currentRevision,
            MaxRevisions = 3,
        };
    }
}
