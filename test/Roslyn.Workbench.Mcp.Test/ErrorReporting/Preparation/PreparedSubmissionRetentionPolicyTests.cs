using System.Collections.Immutable;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting.Preparation;

public sealed class PreparedSubmissionRetentionPolicyTests
{
    [Fact]
    public void GIVEN_ConfiguredPolicy_WHEN_InspectingRetention_THEN_ShouldRejectEviction()
    {
        var options = new ErrorReportingOptions
        {
            PreparedSubmissionCapacity = 20,
        };
        var target = new PreparedSubmissionRetentionPolicy(Options.Create(options));
        var submission = CreateSubmission();
        IReadOnlyDictionary<string, PreparedSubmission> entries =
            new Dictionary<string, PreparedSubmission>
            {
                [submission.Handle] = submission,
            };

        var wasSelected = target.TrySelectEvictionKey(entries, out var key);

        target.Capacity.Should().Be(20);
        target.GetExpiration(submission).Should().Be(submission.ExpiresAt);
        wasSelected.Should().BeFalse();
        key.Should().BeNull();
    }

    private static PreparedSubmission CreateSubmission()
    {
        return new PreparedSubmission
        {
            Handle = "Handle",
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            ExpiresAt = DateTimeOffset.Parse("2000-01-01T00:30:00Z", CultureInfo.InvariantCulture),
            State = PreparedSubmissionState.Prepared,
            Payload = new PreparedDispatchPayload<string>
            {
                DispatcherName = "DispatcherName",
                Destination = "Destination",
                ReportId = "ReportId",
                Report = CreateExternalReport(),
                PreviewBytes = ImmutableArray.Create<byte>(1, 2, 3),
                PreviewJson = "PreviewJson",
                DispatchState = "DispatchState",
            },
        };
    }

    private static ExternalErrorReport CreateExternalReport()
    {
        return new ExternalErrorReport
        {
            ReportId = "ReportId",
            FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            Tool = "Tool",
            ExecutionFamily = "ExecutionFamily",
            PluginClassification = "PluginClassification",
            DurationMilliseconds = 10,
            ExceptionClassification = "ExceptionClassification",
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "OperatingSystem",
            ProcessorArchitecture = "ProcessorArchitecture",
        };
    }
}
