using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.State;

public sealed class WorkspaceSessionStoreTests
{
    private readonly Mock<IWorkspaceQueryCache> _queryCache;

    public WorkspaceSessionStoreTests()
    {
        _queryCache = new Mock<IWorkspaceQueryCache>();
    }

    [Fact]
    public void GIVEN_NewStore_WHEN_ReadingSnapshot_THEN_ShouldReturnEmptySnapshotWithoutOwner()
    {
        var target = new WorkspaceSessionStore(_queryCache.Object);

        var result = target.ReadSnapshot();

        result.Workspaces.Should().BeEmpty();
        result.TransactionOwnerWorkspaceId.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NewStore_WHEN_AllocatingWorkspaceIds_THEN_ShouldReturnMonotonicallyIncreasingIds()
    {
        var target = new WorkspaceSessionStore(_queryCache.Object);

        var first = target.AllocateWorkspaceId();
        var second = target.AllocateWorkspaceId();

        first.Should().Be("workspace-1");
        second.Should().Be("workspace-2");
    }

    [Fact]
    public void GIVEN_NewStore_WHEN_AllocatingWorkspaceEpochs_THEN_ShouldReturnMonotonicallyIncreasingEpochs()
    {
        var target = new WorkspaceSessionStore(_queryCache.Object);

        var first = target.AllocateWorkspaceEpoch();
        var second = target.AllocateWorkspaceEpoch();

        first.Should().Be(1);
        second.Should().Be(2);
    }

    [Fact]
    public void GIVEN_ValidationFailure_WHEN_AddingWorkspace_THEN_ShouldReturnErrorWithoutMutatingSnapshot()
    {
        var target = new WorkspaceSessionStore(_queryCache.Object);
        var session = CreateSession("WorkspaceId", "Alias");
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
        var target = new WorkspaceSessionStore(_queryCache.Object);
        var session = CreateSession("WorkspaceId", "Alias");
        var validate = new Mock<Func<WorkspaceHostSnapshot, WorkspaceOperationError?>>();
        validate.Setup(item => item(It.IsAny<WorkspaceHostSnapshot>())).Returns((WorkspaceOperationError?)null);

        var error = target.TryAddWorkspace(session, validate.Object);
        var result = target.ReadSession("WorkspaceId");

        error.Should().BeNull();
        result.Should().BeSameAs(session);
        target.ReadSession("UnknownWorkspaceId").Should().BeNull();
    }

    [Fact]
    public void GIVEN_UnknownWorkspace_WHEN_Removing_THEN_ShouldReturnNullWithoutChangingSnapshot()
    {
        var target = CreateStoreWithSession(CreateSession("WorkspaceId", "Alias"));
        var snapshot = target.ReadSnapshot();

        var result = target.RemoveWorkspace("UnknownWorkspaceId");

        result.Should().BeNull();
        target.ReadSnapshot().Should().BeSameAs(snapshot);
        _queryCache.Verify(item => item.InvalidateWorkspace(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_OwnerWorkspace_WHEN_Removing_THEN_ShouldRemoveSessionAndClearOwner()
    {
        var session = CreateSession("WorkspaceId", "Alias");
        var target = CreateStoreWithSession(session);
        target.ReplaceSessionAndSetTransactionOwner(session, "WorkspaceId");

        var result = target.RemoveWorkspace("WorkspaceId");

        result.Should().BeSameAs(session);
        target.ReadSnapshot().Workspaces.Should().BeEmpty();
        target.ReadSnapshot().TransactionOwnerWorkspaceId.Should().BeNull();
        _queryCache.Verify(item => item.InvalidateWorkspace("WorkspaceId"), Times.Once);
    }

    [Fact]
    public void GIVEN_NonOwnerWorkspace_WHEN_Removing_THEN_ShouldPreserveDifferentOwner()
    {
        var firstSession = CreateSession("FirstWorkspaceId", "FirstAlias");
        var secondSession = CreateSession("SecondWorkspaceId", "SecondAlias");
        var target = CreateStoreWithSession(firstSession);
        AddSession(target, secondSession);
        target.ReplaceSessionAndSetTransactionOwner(firstSession, "FirstWorkspaceId");

        var result = target.RemoveWorkspace("SecondWorkspaceId");

        result.Should().BeSameAs(secondSession);
        target.ReadSnapshot().TransactionOwnerWorkspaceId.Should().Be("FirstWorkspaceId");
        target.ReadSession("FirstWorkspaceId").Should().BeSameAs(firstSession);
        _queryCache.Verify(item => item.InvalidateWorkspace("SecondWorkspaceId"), Times.Once);
    }

    [Fact]
    public void GIVEN_ReplacementSession_WHEN_Replacing_THEN_ShouldUpdateOnlyMatchingWorkspace()
    {
        var firstSession = CreateSession("FirstWorkspaceId", "FirstAlias");
        var secondSession = CreateSession("SecondWorkspaceId", "SecondAlias");
        var target = CreateStoreWithSession(firstSession);
        AddSession(target, secondSession);
        var replacement = CreateSession("FirstWorkspaceId", "ReplacementAlias");

        target.ReplaceSession(replacement);

        target.ReadSession("FirstWorkspaceId").Should().BeSameAs(replacement);
        target.ReadSession("SecondWorkspaceId").Should().BeSameAs(secondSession);
        _queryCache.Verify(item => item.InvalidateWorkspace(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_ReplacementAndOwner_WHEN_ReplacingAndSettingOwner_THEN_ShouldUpdateBothValues()
    {
        var session = CreateSession("WorkspaceId", "Alias");
        var target = CreateStoreWithSession(session);
        var replacement = CreateSession("WorkspaceId", "ReplacementAlias");

        target.ReplaceSessionAndSetTransactionOwner(replacement, "WorkspaceId");

        target.ReadSession("WorkspaceId").Should().BeSameAs(replacement);
        target.ReadSnapshot().TransactionOwnerWorkspaceId.Should().Be("WorkspaceId");
        _queryCache.Verify(item => item.InvalidateWorkspace(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_ReplacementWithNewSolution_WHEN_Replacing_THEN_ShouldInvalidateWorkspaceCache()
    {
        using var workspace = new AdhocWorkspace();
        var session = CreateSession("WorkspaceId", "Alias", workspace.CurrentSolution);
        var target = CreateStoreWithSession(session);
        var changedSolution = workspace.CurrentSolution.AddProject("Project", "Project", LanguageNames.CSharp).Solution;
        var replacement = CreateSession("WorkspaceId", "Alias", changedSolution);

        target.ReplaceSession(replacement);

        _queryCache.Verify(item => item.InvalidateWorkspace("WorkspaceId"), Times.Once);
    }

    [Theory]
    [InlineData(WorkspaceLifecycleState.WorkspaceOutOfDate)]
    [InlineData(WorkspaceLifecycleState.TransactionConflicted)]
    public void GIVEN_UnavailableReplacement_WHEN_Replacing_THEN_ShouldInvalidateWorkspaceCache(
        WorkspaceLifecycleState state)
    {
        using var workspace = new AdhocWorkspace();
        var session = CreateSession("WorkspaceId", "Alias", workspace.CurrentSolution);
        var target = CreateStoreWithSession(session);
        var replacement = session with
        {
            State = state,
        };

        target.ReplaceSession(replacement);

        _queryCache.Verify(item => item.InvalidateWorkspace("WorkspaceId"), Times.Once);
    }

    [Fact]
    public void GIVEN_PreviousSnapshot_WHEN_StoreChanges_THEN_ShouldRemainUnchanged()
    {
        var firstSession = CreateSession("FirstWorkspaceId", "FirstAlias");
        var target = CreateStoreWithSession(firstSession);
        var previousSnapshot = target.ReadSnapshot();
        var secondSession = CreateSession("SecondWorkspaceId", "SecondAlias");

        AddSession(target, secondSession);

        previousSnapshot.Workspaces.Should().ContainSingle();
        previousSnapshot.Workspaces.Should().ContainKey("FirstWorkspaceId");
        target.ReadSnapshot().Workspaces.Should().HaveCount(2);
    }

    private WorkspaceSessionStore CreateStoreWithSession(WorkspaceSessionSnapshot session)
    {
        var target = new WorkspaceSessionStore(_queryCache.Object);
        AddSession(target, session);
        return target;
    }

    private static void AddSession(WorkspaceSessionStore target, WorkspaceSessionSnapshot session)
    {
        var validate = new Mock<Func<WorkspaceHostSnapshot, WorkspaceOperationError?>>();
        validate.Setup(item => item(It.IsAny<WorkspaceHostSnapshot>())).Returns((WorkspaceOperationError?)null);
        target.TryAddWorkspace(session, validate.Object).Should().BeNull();
    }

    private static WorkspaceSessionSnapshot CreateSession(string workspaceId, string alias, Solution? solution = null)
    {
        return new WorkspaceSessionSnapshot
        {
            State = WorkspaceLifecycleState.Ready,
            Workspace = new WorkspaceIdentity
            {
                WorkspaceId = workspaceId,
                Alias = alias,
                LoadedPath = "LoadedPath",
            },
            LoadedWorkspace = null!,
            CurrentSolution = solution!,
            InputManifest = null!,
            OperationGate = null!,
        };
    }
}
