using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Operations;

public sealed class WorkspaceOperationContextFactoryTests : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();

    [Fact]
    public void GIVEN_SessionWithTransaction_WHEN_CreatingOperationAndFailureContexts_THEN_ShouldCaptureExpectedState()
    {
        var session = CreateSession(transactionRevision: 1);
        var expectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            session.CurrentSnapshotIdentity,
            session.Transaction?.CurrentRevision);

        var operationContext = WorkspaceOperationContextFactory.Create(session);
        var failureContext = WorkspaceFailureContextFactory.Create(session);

        operationContext.Snapshot.Should().Be(expectedSnapshot);
        operationContext.WorkspaceId.Should().Be(expectedSnapshot.WorkspaceId);
        operationContext.WorkspaceEpoch.Should().Be(expectedSnapshot.WorkspaceEpoch);
        operationContext.TransactionRevision.Should().Be(expectedSnapshot.TransactionRevision);
        failureContext.Workspace.Should().BeSameAs(session.Workspace);
        failureContext.LifecycleState.Should().Be(WorkspaceLifecycleState.TransactionActive);
        failureContext.ProjectCount.Should().Be(session.ProjectCount);
        failureContext.DocumentCount.Should().Be(session.DocumentCount);
        failureContext.TransactionRevision.Should().Be(session.Transaction?.CurrentRevision);
    }

    [Fact]
    public void GIVEN_SessionWithoutTransaction_WHEN_CreatingContext_THEN_ShouldUseNullRevision()
    {
        var session = CreateSession(transactionRevision: null);
        var expectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            session.CurrentSnapshotIdentity,
            transactionRevision: null);

        var result = WorkspaceOperationContextFactory.Create(session);

        result.Snapshot.Should().Be(expectedSnapshot);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private WorkspaceSessionSnapshot CreateSession(int? transactionRevision)
    {
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 2,
        };
        var snapshotId = WorkspaceSnapshotTestFactory.CreateId(3);
        WorkspaceTransaction? transaction = null;
        if (transactionRevision is not null)
        {
            var preview = new MutationPreview
            {
                Summary = "Summary",
            };

            var revision = new WorkspaceTransactionRevision
            {
                SnapshotId = WorkspaceSnapshotTestFactory.CreateId(4),
                Solution = _workspace.CurrentSolution,
                Changes = new ChangeSummary(),
                Operation = "Operation",
                Summary = "Summary",
                Preview = preview,
            };

            transaction = new WorkspaceTransaction
            {
                TransactionId = new WorkspaceTransactionId(4),
                BaselineSnapshotId = snapshotId,
                BaselineSolution = _workspace.CurrentSolution,
                Revisions = [revision],
                CurrentRevision = transactionRevision.Value,
                MaxRevisions = 10,
            };
        }

        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var operationGate = new Mock<IWorkspaceOperationGate>();
        var inputManifest = new WorkspaceInputManifest();

        var snapshotIdentity = WorkspaceSnapshotIdentity.Create(
            workspaceIdentity,
            snapshotId,
            transaction);

        var session = new WorkspaceSessionSnapshot
        {
            Workspace = workspaceIdentity,
            CommittedSnapshotId = snapshotId,
            State = transaction is null
                ? WorkspaceLifecycleState.Ready
                : WorkspaceLifecycleState.TransactionActive,
            LoadedWorkspace = loadedWorkspace.Object,
            CurrentSnapshotIdentity = snapshotIdentity,
            CurrentSolution = _workspace.CurrentSolution,
            Transaction = transaction,
            InputManifest = inputManifest,
            OperationGate = operationGate.Object,
        };

        return session;
    }
}
