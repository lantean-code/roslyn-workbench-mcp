using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Test.Tools;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class GetErrorDetailsToolTests
{
    [Fact]
    public async Task GIVEN_CapturedCorrelationId_WHEN_GettingDetails_THEN_ShouldReturnLocalUnsafeDiagnostic()
    {
        var correlationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var store = new Mock<ICapturedErrorStore>();
        var record = CreateRecord(correlationId);
        CapturedErrorRecord? storedRecord = record;
        store
            .Setup(item => item.TryGet(correlationId, out storedRecord))
            .Returns(true);
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new GetErrorDetailsTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            store.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "get-error-details",
            new Dictionary<string, JsonElement>
            {
                ["correlationId"] = JsonSerializer.SerializeToElement(correlationId),
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("sensitivity").GetString().Should().Be("LocalDiagnostic");
        data.GetProperty("safeForExternalSubmission").GetBoolean().Should().BeFalse();
        data.GetProperty("error").GetProperty("correlationId").GetGuid().Should().Be(correlationId);
    }

    [Fact]
    public async Task GIVEN_UnknownOrExpiredCorrelationId_WHEN_GettingDetails_THEN_ShouldReject()
    {
        var correlationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var store = new Mock<ICapturedErrorStore>();
        CapturedErrorRecord? storedRecord = null;
        store
            .Setup(item => item.TryGet(correlationId, out storedRecord))
            .Returns(false);
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new GetErrorDetailsTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            store.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "get-error-details",
            new Dictionary<string, JsonElement>
            {
                ["correlationId"] = JsonSerializer.SerializeToElement(correlationId),
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value
            .GetProperty("error")
            .GetProperty("code")
            .GetString()
            .Should()
            .Be("ErrorDetailsUnavailable");
    }

    [Fact]
    public async Task GIVEN_CorrelationIdIsMissing_WHEN_GettingDetails_THEN_ShouldRejectDuringRequestBinding()
    {
        var store = new Mock<ICapturedErrorStore>();
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new GetErrorDetailsTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            store.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "get-error-details",
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value
            .GetProperty("error")
            .GetProperty("code")
            .GetString()
            .Should()
            .Be("InvalidRequest");
        result.StructuredContent.Value
            .GetProperty("error")
            .GetProperty("message")
            .GetString()
            .Should()
            .Be("Missing required tool argument: 'correlationId'.");
        store.Verify(
            item => item.TryGet(
                It.IsAny<Guid>(),
                out It.Ref<CapturedErrorRecord?>.IsAny),
            Times.Never);
    }

    private static CapturedErrorRecord CreateRecord(Guid correlationId)
    {
        return new CapturedErrorRecord
        {
            CorrelationId = correlationId,
            FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            ExpiresAt = DateTimeOffset.Parse("2000-01-01T01:00:00Z", CultureInfo.InvariantCulture),
            ToolName = "server-status",
            ExecutionFamily = "ServerOwned",
            PluginClassification = "Host",
            DurationMilliseconds = 1,
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "OperatingSystem",
            ProcessorArchitecture = "ProcessorArchitecture",
        };
    }
}
