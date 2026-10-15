using Microsoft.Extensions.Logging;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class HostStartupStorageTests
{
    [Fact]
    public void GIVEN_WritableStorage_WHEN_Initialising_THEN_ShouldAllowHostStartup()
    {
        var stateDirectory = new Mock<IWorkspaceStateDirectory>();
        var logger = new Mock<ILogger>();
        using var output = new StringWriter();

        var succeeded = HostStartupStorage.TryInitialize(stateDirectory.Object, logger.Object, output);

        succeeded.Should().BeTrue();
        stateDirectory.Verify(directory => directory.Initialize(), Times.Once);
        output.ToString().Should().BeEmpty();
        logger.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GIVEN_StorageAccessFailure_WHEN_Initialising_THEN_ShouldReportRemedyWithoutStackTrace(int kind)
    {
        var failure = CreateStorageFailure(kind);
        var stateDirectory = new Mock<IWorkspaceStateDirectory>();
        stateDirectory.Setup(directory => directory.Initialize()).Throws(failure);
        stateDirectory.SetupGet(directory => directory.RecoveryDirectory).Returns("RecoveryDirectory");
        var logger = new Mock<ILogger>();
        using var output = new StringWriter();

        var succeeded = HostStartupStorage.TryInitialize(stateDirectory.Object, logger.Object, output);

        succeeded.Should().BeFalse();
        output.ToString().Should().Contain("Cannot initialise durable recovery storage at 'RecoveryDirectory'")
            .And.Contain(failure.Message)
            .And.Contain("--state-directory")
            .And.NotContain(failure.GetType().Name);
        logger.Verify(item => item.IsEnabled(LogLevel.Debug), Times.Once);
        logger.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("StateDirectory")]
    [InlineData("RecoveryDirectory")]
    public void GIVEN_UnsafeUnixPermissions_WHEN_Initialising_THEN_ShouldPreservePermissionRemedyWithoutStackTrace(string directoryPath)
    {
        var message = $"The Workspace state directory '{directoryPath}' must use Unix permissions '700'.";
        var failure = new UnauthorizedAccessException(message);
        var stateDirectory = new Mock<IWorkspaceStateDirectory>();
        stateDirectory.Setup(directory => directory.Initialize()).Throws(failure);
        stateDirectory.SetupGet(directory => directory.RecoveryDirectory).Returns("RecoveryDirectory");
        var logger = new Mock<ILogger>();
        using var output = new StringWriter();

        var succeeded = HostStartupStorage.TryInitialize(stateDirectory.Object, logger.Object, output);

        succeeded.Should().BeFalse();
        var diagnostic = output.ToString();
        diagnostic.Should().Contain(message)
            .And.Contain("--state-directory")
            .And.Contain("Logging__LogLevel__Default=Debug")
            .And.NotContain("UnauthorizedAccessException")
            .And.NotContain("   at ");
        diagnostic.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle();
        logger.Verify(item => item.IsEnabled(LogLevel.Debug), Times.Once);
        logger.VerifyNoOtherCalls();
    }

    [Fact]
    public void GIVEN_DebugLogging_WHEN_StorageFails_THEN_ShouldRetainOriginalException()
    {
        var failure = new IOException("Message");
        var stateDirectory = new Mock<IWorkspaceStateDirectory>();
        stateDirectory.Setup(directory => directory.Initialize()).Throws(failure);
        var logger = new Mock<ILogger>();
        logger.Setup(item => item.IsEnabled(LogLevel.Debug)).Returns(true);
        using var output = new StringWriter();

        var succeeded = HostStartupStorage.TryInitialize(stateDirectory.Object, logger.Object, output);

        succeeded.Should().BeFalse();
#pragma warning disable CA1873 // Moq inspects the expression tree rather than executing the logging call.
        logger.Verify(item => item.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            failure,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
#pragma warning restore CA1873
    }

    [Fact]
    public void GIVEN_UnexpectedStorageFailure_WHEN_Initialising_THEN_ShouldPropagateFailure()
    {
        var failure = new InvalidOperationException("Message");
        var stateDirectory = new Mock<IWorkspaceStateDirectory>();
        stateDirectory.Setup(directory => directory.Initialize()).Throws(failure);
        var logger = new Mock<ILogger>();
        using var output = new StringWriter();

        var action = () => HostStartupStorage.TryInitialize(stateDirectory.Object, logger.Object, output);

        action.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(failure);
        output.ToString().Should().BeEmpty();
        logger.VerifyNoOtherCalls();
    }

    private static Exception CreateStorageFailure(int kind)
    {
        var ioFailure = new IOException("Message");
        var accessFailure = new UnauthorizedAccessException("Message");
        var wrappedIoFailure = new InvalidOperationException("Message", ioFailure);
        var wrappedAccessFailure = new InvalidOperationException("Message", accessFailure);
        return kind switch
        {
            0 => ioFailure,
            1 => accessFailure,
            2 => wrappedIoFailure,
            _ => wrappedAccessFailure,
        };
    }
}
