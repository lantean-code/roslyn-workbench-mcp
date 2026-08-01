namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ErrorReportingConsentServiceTests
{
    [Fact]
    public void GIVEN_StoredConsentState_WHEN_GettingState_THEN_ShouldReturnStoredState()
    {
        var store = new Mock<IErrorReportingConsentStore>();
        store
            .Setup(item => item.GetState(Guid.Parse("11111111-1111-1111-1111-111111111111"), 5))
            .Returns(ErrorReportingConsentState.AllowedForWorkspace);

        var target = new ErrorReportingConsentService(store.Object);

        var result = target.GetState(Guid.Parse("11111111-1111-1111-1111-111111111111"), 5);

        result.Should().Be(ErrorReportingConsentState.AllowedForWorkspace);
    }

    [Fact]
    public void GIVEN_WorkspaceScope_WHEN_AllowingWorkspace_THEN_ShouldStoreGrant()
    {
        var store = new Mock<IErrorReportingConsentStore>();
        var target = new ErrorReportingConsentService(store.Object);

        target.AllowWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111"), 5);

        store.Verify(item => item.AllowWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111"), 5), Times.Once);
    }

    [Fact]
    public void GIVEN_ConsentService_WHEN_AllowingSession_THEN_ShouldStoreGrant()
    {
        var store = new Mock<IErrorReportingConsentStore>();
        var target = new ErrorReportingConsentService(store.Object);

        target.AllowSession();

        store.Verify(item => item.AllowSession(), Times.Once);
    }

    [Fact]
    public void GIVEN_ConsentService_WHEN_SuppressingSession_THEN_ShouldStoreSuppression()
    {
        var store = new Mock<IErrorReportingConsentStore>();
        var target = new ErrorReportingConsentService(store.Object);

        target.SuppressSession();

        store.Verify(item => item.SuppressSession(), Times.Once);
    }
}
