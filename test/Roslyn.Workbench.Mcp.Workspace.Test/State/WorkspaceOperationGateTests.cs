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
    public async Task GIVEN_SharedLeasesAtLimit_WHEN_AcquiringAnotherSharedLease_THEN_ShouldRejectUntilLeaseReleased()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 2);
        var firstLease = target.TryAcquireShared();
        var secondLease = target.TryAcquireShared();

        var rejectedLease = target.TryAcquireShared();

        firstLease.Should().NotBeNull();
        secondLease.Should().NotBeNull();
        rejectedLease.Should().BeNull();

        await firstLease!.DisposeAsync();
        var replacementLease = target.TryAcquireShared();
        replacementLease.Should().NotBeNull();

        await secondLease!.DisposeAsync();
        await replacementLease!.DisposeAsync();
    }

    [Fact]
    public async Task GIVEN_SharedLease_WHEN_AcquiringExclusiveLease_THEN_ShouldRejectUntilSharedLeaseReleased()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 1);
        var sharedLease = target.TryAcquireShared();

        var rejectedLease = target.TryAcquireExclusive();

        rejectedLease.Should().BeNull();
        await sharedLease!.DisposeAsync();
        var exclusiveLease = target.TryAcquireExclusive();
        exclusiveLease.Should().NotBeNull();
        await exclusiveLease!.DisposeAsync();
    }

    [Fact]
    public async Task GIVEN_ExclusiveLease_WHEN_AcquiringSharedLease_THEN_ShouldRejectUntilExclusiveLeaseReleased()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 1);
        var exclusiveLease = target.TryAcquireExclusive();

        var rejectedLease = target.TryAcquireShared();

        rejectedLease.Should().BeNull();
        await exclusiveLease!.DisposeAsync();
        var sharedLease = target.TryAcquireShared();
        sharedLease.Should().NotBeNull();
        await sharedLease!.DisposeAsync();
    }

    [Fact]
    public async Task GIVEN_ExclusiveLease_WHEN_AcquiringAnotherExclusiveLease_THEN_ShouldRejectUntilLeaseReleased()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 1);
        var exclusiveLease = target.TryAcquireExclusive();

        var rejectedLease = target.TryAcquireExclusive();

        rejectedLease.Should().BeNull();
        await exclusiveLease!.DisposeAsync();
        var replacementLease = target.TryAcquireExclusive();
        replacementLease.Should().NotBeNull();
        await replacementLease!.DisposeAsync();
    }

    [Fact]
    public async Task GIVEN_SharedLeaseDisposedTwice_WHEN_AcquiringAtLimit_THEN_ShouldNotCorruptLeaseCount()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 1);
        var lease = target.TryAcquireShared();

        await lease!.DisposeAsync();
        await lease.DisposeAsync();
        var replacementLease = target.TryAcquireShared();
        var rejectedLease = target.TryAcquireShared();

        replacementLease.Should().NotBeNull();
        rejectedLease.Should().BeNull();
        await replacementLease!.DisposeAsync();
    }

    [Fact]
    public async Task GIVEN_ExclusiveLeaseDisposedTwice_WHEN_AcquiringExclusive_THEN_ShouldRemainAvailableOnce()
    {
        var target = new WorkspaceOperationGate(maxConcurrentQueries: 1);
        var lease = target.TryAcquireExclusive();

        await lease!.DisposeAsync();
        await lease.DisposeAsync();
        var replacementLease = target.TryAcquireExclusive();
        var rejectedLease = target.TryAcquireExclusive();

        replacementLease.Should().NotBeNull();
        rejectedLease.Should().BeNull();
        await replacementLease!.DisposeAsync();
    }
}
