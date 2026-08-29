using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ExternalErrorReportRedactorTests
{
    [Fact]
    public void GIVEN_ReportWithExceptionMessages_WHEN_Redacting_THEN_ShouldRemoveOnlyMessages()
    {
        var frame = new ExternalStackFrame
        {
            Component = ErrorReportComponent.RoslynWorkbench,
            Method = "ExecuteAsync",
        };
        var report = new ExternalErrorReport
        {
            ReportId = "ReportId",
            FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            Tool = "server-status",
            ExecutionFamily = "ServerOwned",
            PluginClassification = "Host",
            DurationMilliseconds = 25,
            ExceptionClassification = "DotNetException",
            Exceptions =
            [
                new ExternalException
                {
                    Type = "System.InvalidOperationException",
                    Message = "Sensitive message",
                    StackFrames = ImmutableArray.Create(frame),
                },
            ],
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "Linux",
            ProcessorArchitecture = "X64",
        };

        var result = ExternalErrorReportRedactor.RemoveExceptionMessages(report);

        result.Should().NotBeSameAs(report);
        result.Exceptions.Should().ContainSingle();
        result.Exceptions[0].Message.Should().BeNull();
        result.Exceptions[0].Type.Should().Be(report.Exceptions[0].Type);
        result.Exceptions[0].StackFrames.Should().Equal(frame);
        report.Exceptions[0].Message.Should().Be("Sensitive message");
    }
}
