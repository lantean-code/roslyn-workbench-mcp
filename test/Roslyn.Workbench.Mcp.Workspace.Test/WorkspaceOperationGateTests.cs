using AwesomeAssertions;

using Roslyn.Workbench.Mcp.Workspace;

using Xunit;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceOperationGateTests
{
    [Fact]
    public async Task GIVEN_SharedLeaseAtConcurrencyLimit_WHEN_AcquiringAdditionalSharedLease_THEN_ShouldReturnBusy()
    {
        var target = new WorkspaceOperationGate(1);
        var sharedLease = target.TryAcquireShared();

        sharedLease.Should().NotBeNull();
        await using var lease = sharedLease!;

        var blockedLease = target.TryAcquireShared();

        blockedLease.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_SharedLeaseInFlight_WHEN_AcquiringExclusiveLease_THEN_ShouldReturnBusy()
    {
        var target = new WorkspaceOperationGate(2);
        var sharedLease = target.TryAcquireShared();

        sharedLease.Should().NotBeNull();
        await using var lease = sharedLease!;

        var blockedLease = target.TryAcquireExclusive();

        blockedLease.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_ReleasedSharedLease_WHEN_AcquiringExclusiveLease_THEN_ShouldSucceed()
    {
        var target = new WorkspaceOperationGate(1);
        var sharedLease = target.TryAcquireShared();
        sharedLease.Should().NotBeNull();
        await sharedLease!.DisposeAsync();

        var exclusiveLease = target.TryAcquireExclusive();

        exclusiveLease.Should().NotBeNull();

        if (exclusiveLease is not null)
        {
            await exclusiveLease.DisposeAsync();
        }
    }
}
