namespace Roslyn.Workbench.Mcp.Workspace.Test.State;

public sealed class WorkspaceOperationGateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GIVEN_NonPositiveQueryLimit_WHEN_CreatingGate_THEN_ShouldThrowArgumentOutOfRangeException(int maxConcurrentQueries)
    {
        var action = () => new WorkspaceOperationGate(maxConcurrentQueries);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GIVEN_SharedLeasesAtLimit_WHEN_AcquiringAnotherSharedLease_THEN_ShouldRejectUntilLeaseReleased()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 2);
        var firstLease = target.TryAcquireShared();
        var secondLease = target.TryAcquireShared();

        var rejectedLease = target.TryAcquireShared();

        firstLease.Should().NotBeNull();
        secondLease.Should().NotBeNull();
        rejectedLease.Should().BeNull();

        firstLease!.Dispose();
        var replacementLease = target.TryAcquireShared();
        replacementLease.Should().NotBeNull();

        secondLease!.Dispose();
        replacementLease!.Dispose();
    }

    [Fact]
    public void GIVEN_SharedLease_WHEN_AcquiringExclusiveLease_THEN_ShouldRejectUntilSharedLeaseReleased()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 1);
        var sharedLease = target.TryAcquireShared();

        var rejectedLease = target.TryAcquireExclusive();

        rejectedLease.Should().BeNull();
        sharedLease!.Dispose();
        var exclusiveLease = target.TryAcquireExclusive();
        exclusiveLease.Should().NotBeNull();
        exclusiveLease!.Dispose();
    }

    [Fact]
    public void GIVEN_ExclusiveLease_WHEN_AcquiringSharedLease_THEN_ShouldRejectUntilExclusiveLeaseReleased()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 1);
        var exclusiveLease = target.TryAcquireExclusive();

        var rejectedLease = target.TryAcquireShared();

        rejectedLease.Should().BeNull();
        exclusiveLease!.Dispose();
        var sharedLease = target.TryAcquireShared();
        sharedLease.Should().NotBeNull();
        sharedLease!.Dispose();
    }

    [Fact]
    public void GIVEN_ExclusiveLease_WHEN_AcquiringAnotherExclusiveLease_THEN_ShouldRejectUntilLeaseReleased()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 1);
        var exclusiveLease = target.TryAcquireExclusive();

        var rejectedLease = target.TryAcquireExclusive();

        rejectedLease.Should().BeNull();
        exclusiveLease!.Dispose();
        var replacementLease = target.TryAcquireExclusive();
        replacementLease.Should().NotBeNull();
        replacementLease!.Dispose();
    }

    [Fact]
    public void GIVEN_SharedLeaseDisposedTwice_WHEN_AcquiringAtLimit_THEN_ShouldNotCorruptLeaseCount()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 1);
        var lease = target.TryAcquireShared();

        lease!.Dispose();
        lease.Dispose();
        var replacementLease = target.TryAcquireShared();
        var rejectedLease = target.TryAcquireShared();

        replacementLease.Should().NotBeNull();
        rejectedLease.Should().BeNull();
        replacementLease!.Dispose();
    }

    [Fact]
    public void GIVEN_ExclusiveLeaseDisposedTwice_WHEN_AcquiringExclusive_THEN_ShouldRemainAvailableOnce()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 1);
        var lease = target.TryAcquireExclusive();

        lease!.Dispose();
        lease.Dispose();
        var replacementLease = target.TryAcquireExclusive();
        var rejectedLease = target.TryAcquireExclusive();

        replacementLease.Should().NotBeNull();
        rejectedLease.Should().BeNull();
        replacementLease!.Dispose();
    }
}
