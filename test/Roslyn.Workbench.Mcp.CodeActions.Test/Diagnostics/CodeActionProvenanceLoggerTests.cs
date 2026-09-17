using Microsoft.Extensions.Logging;
using Roslyn.Workbench.Mcp.CodeActions.Diagnostics;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Diagnostics;

public sealed class CodeActionProvenanceLoggerTests
{
    [Fact]
    public void GIVEN_PreparedFixAll_WHEN_Logging_THEN_ShouldWriteStructuredPreparationEvent()
    {
        var logger = CreateEnabledLogger();
        var target = new CodeActionProvenanceLogger(logger.Object);

        target.LogPreparedFixAll(CreateProvenance());

        VerifyEvent(logger, 1, "Prepared Fix All", "A001,Z001");
    }

    [Fact]
    public void GIVEN_StagedAction_WHEN_Logging_THEN_ShouldWriteStructuredStagingEvent()
    {
        var logger = CreateEnabledLogger();
        var target = new CodeActionProvenanceLogger(logger.Object);

        target.LogStaged(CreateProvenance());

        VerifyEvent(logger, 2, "Staged Code Action FixAll", "A001,Z001");
    }

    [Fact]
    public void GIVEN_InformationLoggingIsDisabled_WHEN_LoggingProvenance_THEN_ShouldNotEvaluateOrWriteEvents()
    {
        var logger = new Mock<ILogger<CodeActionProvenanceLogger>>();
        var target = new CodeActionProvenanceLogger(logger.Object);
        var provenance = CreateProvenance();

        target.LogPreparedFixAll(provenance);
        target.LogStaged(provenance);

        logger.Verify(item => item.IsEnabled(LogLevel.Information), Times.Exactly(2));
        logger.VerifyNoOtherCalls();
    }

    private static Mock<ILogger<CodeActionProvenanceLogger>> CreateEnabledLogger()
    {
        var logger = new Mock<ILogger<CodeActionProvenanceLogger>>();
        logger.Setup(item => item.IsEnabled(LogLevel.Information)).Returns(true);
        return logger;
    }

    private static void VerifyEvent(
        Mock<ILogger<CodeActionProvenanceLogger>> logger,
        int eventId,
        string expectedMessage,
        string expectedDiagnosticIds)
    {
#pragma warning disable CA1873 // Moq analyses the expression tree; it does not invoke ILogger.Log here.
        logger.Verify(item => item.Log(
            LogLevel.Information,
            It.Is<EventId>(value => value.Id == eventId),
            It.Is<It.IsAnyType>((state, _) =>
                state.ToString()!.Contains(expectedMessage, StringComparison.Ordinal)
                && state.ToString()!.Contains(expectedDiagnosticIds, StringComparison.Ordinal)),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
#pragma warning restore CA1873
    }

    private static CodeActionMutationProvenance CreateProvenance()
    {
        var provider = CodeActionExecutionTestFactory.CreateProviderIdentity();
        var fixAllProvider = CodeActionExecutionTestFactory.CreateProviderIdentity() with
        {
            TypeName = "FixAll.Provider",
        };

        return new CodeActionMutationProvenance
        {
            Kind = CodeActionMutationKind.FixAll,
            Provider = provider,
            FixAllProvider = fixAllProvider,
            DiagnosticIds = ["A001", "Z001"],
            EquivalenceKey = "EquivalenceKey",
            FixAllScope = "Solution",
        };
    }
}
