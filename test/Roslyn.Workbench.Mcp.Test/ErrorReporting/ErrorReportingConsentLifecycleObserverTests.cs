namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ErrorReportingConsentLifecycleObserverTests
{
    private readonly Mock<IErrorReportingConsentStore> _store;
    private readonly ErrorReportingConsentLifecycleObserver _target;

    public ErrorReportingConsentLifecycleObserverTests()
    {
        _store = new Mock<IErrorReportingConsentStore>();
        _target = new ErrorReportingConsentLifecycleObserver(_store.Object);
    }

    [Fact]
    public void GIVEN_WorkspaceSnapshot_WHEN_InvalidatingWorkspace_THEN_ShouldInvalidateWorkspaceGrant()
    {
        _target.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111"), 5);

        _store.Verify(item => item.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111"), 5), Times.Once);
    }

    [Fact]
    public void GIVEN_TransactionSnapshot_WHEN_InvalidatingTransaction_THEN_ShouldRetainWorkspaceGrant()
    {
        var transactionId = new WorkspaceTransactionId(1);

        _target.InvalidateTransaction(Guid.Parse("11111111-1111-1111-1111-111111111111"), 5, transactionId);

        _store.VerifyNoOtherCalls();
    }

    [Fact]
    public void GIVEN_OrdinarySnapshots_WHEN_InvalidatingSnapshots_THEN_ShouldRetainWorkspaceGrant()
    {
        _target.InvalidateSnapshots([]);

        _store.VerifyNoOtherCalls();
    }
}
