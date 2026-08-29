using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ErrorReporting.Configuration;
using Roslyn.Workbench.Mcp.ErrorReporting.Contracts;
using Roslyn.Workbench.Mcp.ErrorReporting.Preparation;
using Roslyn.Workbench.Mcp.ErrorReporting.Projection;
using Roslyn.Workbench.Mcp.ErrorReporting.Retention;
using Roslyn.Workbench.Mcp.ErrorReporting.Tools;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ConcurrentSubmissionIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_OnePreparedReport_WHEN_TwoSubmissionsOverlap_THEN_ShouldDispatchExactlyOnce()
    {
        var now = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture);
        var timeProvider = new Mock<TimeProvider>();
        var expirationTimer = new Mock<ITimer>();
        timeProvider.Setup(item => item.GetUtcNow()).Returns(now);
        timeProvider
            .Setup(item => item.CreateTimer(
                It.IsAny<TimerCallback>(),
                It.IsAny<object?>(),
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan))
            .Returns(expirationTimer.Object);
        expirationTimer
            .Setup(item => item.Change(It.IsAny<TimeSpan>(), Timeout.InfiniteTimeSpan))
            .Returns(true);
        var options = Options.Create(new ErrorReportingOptions());
        var retentionPolicy = new PreparedSubmissionRetentionPolicy(options);
        await using var entries = new BoundedExpiringStore<string, PreparedSubmission>(
            retentionPolicy,
            timeProvider.Object);
        var store = new PreparedSubmissionStore(entries);
        var submission = CreateSubmission(now);
        store.TryAdd(submission).Should().BeTrue();

        var requestBinder = new Mock<IToolRequestBinder>();
        var request = new SubmitErrorReportRequest { SubmissionHandle = submission.Handle };
        string? bindingError = null;
        requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out request,
                out bindingError))
            .Returns(true);
        var consentService = new Mock<IErrorReportingConsentService>();
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.AlwaysApproved);
        var dispatchEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = new Mock<IErrorReportDispatcher>();
        dispatcher
            .Setup(item => item.DispatchAsync(
                submission.Payload,
                ExceptionMessageHandling.Include,
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                dispatchEntered.SetResult();
                await releaseDispatch.Task;
                return new ErrorDispatchResult
                {
                    Outcome = ErrorDispatchOutcome.Accepted,
                    ReportReference = "ReportReference",
                    PayloadDigest = "PayloadDigest",
                };
            });
        var protocolFactory = new Mock<IMcpToolProtocolFactory>();
        protocolFactory.SetReturnsDefault(new Tool { Name = "Name" });
        var target = new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            requestBinder.Object,
            store,
            consentService.Object,
            dispatcher.Object);
        var server = new Mock<McpServer>();
        var firstCall = target.InvokeAsync(
            CreateRequestContext(server.Object, submission.Handle),
            TestContext.Current.CancellationToken).AsTask();
        await dispatchEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        var secondResult = await target.InvokeAsync(
            CreateRequestContext(server.Object, submission.Handle),
            TestContext.Current.CancellationToken);
        releaseDispatch.SetResult();
        var firstResult = await firstCall;

        firstResult.IsError.Should().NotBeTrue();
        secondResult.IsError.Should().BeTrue();
        secondResult.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("ErrorReportSubmissionInProgress");
        dispatcher.Verify(item => item.DispatchAsync(
            submission.Payload,
            ExceptionMessageHandling.Include,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static RequestContext<CallToolRequestParams> CreateRequestContext(
        McpServer server,
        string handle)
    {
        return new RequestContext<CallToolRequestParams>(
            server,
            new JsonRpcRequest
            {
                Method = RequestMethods.ToolsCall,
            },
            new CallToolRequestParams
            {
                Name = "submit-error-report",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["submissionHandle"] = JsonSerializer.SerializeToElement(handle),
                },
            });
    }

    private static PreparedSubmission CreateSubmission(DateTimeOffset createdAt)
    {
        var report = new ExternalErrorReport
        {
            ReportId = "ReportId",
            FailureTime = createdAt,
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
        var payload = new PreparedDispatchPayload<string>
        {
            DispatcherName = "DispatcherName",
            Destination = "Destination",
            ReportId = report.ReportId,
            Report = report,
            PreviewBytes = ImmutableArray.Create<byte>(1, 2, 3),
            PreviewJson = "PreviewJson",
            DispatchState = "DispatchState",
        };

        return new PreparedSubmission
        {
            Handle = "Handle",
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = createdAt,
            ExpiresAt = createdAt.AddMinutes(30),
            State = PreparedSubmissionState.Prepared,
            Payload = payload,
        };
    }
}
