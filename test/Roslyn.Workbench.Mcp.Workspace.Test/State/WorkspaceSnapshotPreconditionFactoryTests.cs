namespace Roslyn.Workbench.Mcp.Workspace.Test.State;

public sealed class WorkspaceSnapshotPreconditionFactoryTests
{
    [Fact]
    public void GIVEN_SnapshotIdentityAndRevision_WHEN_CreatingPrecondition_THEN_ShouldMapAllValues()
    {
        var snapshotIdentity = new WorkspaceSnapshotIdentity(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            2,
            WorkspaceSnapshotTestFactory.CreateId(3),
            new WorkspaceTransactionId(4));

        var result = WorkspaceSnapshotPreconditionFactory.Create(snapshotIdentity, transactionRevision: 5);

        result.WorkspaceId.Should().Be(snapshotIdentity.WorkspaceId);
        result.WorkspaceEpoch.Should().Be(snapshotIdentity.WorkspaceEpoch);
        result.SnapshotId.Should().Be(snapshotIdentity.SnapshotId.Value);
        result.TransactionRevision.Should().Be(5);
    }
}
