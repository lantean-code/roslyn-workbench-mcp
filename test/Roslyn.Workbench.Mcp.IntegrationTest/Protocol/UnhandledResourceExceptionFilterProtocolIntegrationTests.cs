using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using Roslyn.Workbench.Mcp.ErrorReporting.Projection;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class UnhandledResourceExceptionFilterProtocolIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Integration")]
    public async Task GIVEN_RegisteredResourceFails_WHEN_ReadingOverMcp_THEN_ShouldExposeCorrelatedDetailsAndAllowReportPreparation(bool protocolException)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath(), "--error-reporting-consent", "always"]);
        builder.Logging.ClearProviders();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var previewDispatcher = new LoggingErrorReportDispatcher(NullLogger<LoggingErrorReportDispatcher>.Instance);
        dispatcher.SetupGet(item => item.Name).Returns(previewDispatcher.Name);
        dispatcher.SetupGet(item => item.Destination).Returns(previewDispatcher.Destination);
        dispatcher.Setup(item => item.CreatePayload(It.IsAny<ExternalErrorReport>()))
            .Returns((ExternalErrorReport report) => previewDispatcher.CreatePayload(report));
        builder.Services.AddSingleton(dispatcher.Object);
        var resourceOptions = new McpServerResourceCreateOptions
        {
            Name = "failing-resource",
            UriTemplate = "probe://sensitive-project/resource",
        };
        Exception exception = protocolException
            ? new McpProtocolException("Sensitive exception message", McpErrorCode.ResourceNotFound)
            : new InvalidOperationException("Sensitive exception message");
        var resource = McpServerResource.Create((Func<string>)(() => throw exception), resourceOptions);
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        builder.Services.AddMcpServer()
            .WithResources([resource])
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream());

        await using var services = builder.Services.BuildServiceProvider(validateScopes: true);
        var server = services.GetRequiredService<McpServer>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(20));
        var serverTask = server.RunAsync(cancellation.Token);
        var transport = new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream(), NullLoggerFactory.Instance);
        await using var client = await McpClient.CreateAsync(transport, loggerFactory: NullLoggerFactory.Instance, cancellationToken: cancellation.Token);

        try
        {
            var read = async () => await client.ReadResourceAsync(new Uri("probe://sensitive-project/resource"), cancellationToken: cancellation.Token);
            var failure = await read.Should().ThrowAsync<McpProtocolException>();
            failure.Which.ErrorCode.Should().Be(McpErrorCode.InternalError);
            failure.Which.Message.Should().NotContain("Sensitive");
            var error = failure.Which.Data["error"].Should().BeOfType<JsonElement>().Subject;
            var id = error.GetProperty("correlationId").GetGuid();
            error.GetProperty("code").GetString().Should().Be("UnhandledException");
            var diagnostics = failure.Which.Data["diagnostics"].Should().BeOfType<JsonElement>().Subject;
            diagnostics.GetProperty("detailsTool").GetString().Should().Be("get-error-details");

            var arguments = new Dictionary<string, object?> { ["correlationId"] = id.ToString() };
            var details = await client.CallToolAsync("get-error-details", arguments, cancellationToken: cancellation.Token);
            details.IsError.Should().NotBeTrue();
            var diagnosticData = details.StructuredContent.GetValueOrDefault().GetProperty("data");
            var record = diagnosticData.GetProperty("error");
            record.GetProperty("correlationId").GetGuid().Should().Be(id);
            record.GetProperty("toolName").GetString().Should().Be("resources/read");
            record.GetProperty("executionFamily").GetString().Should().Be("Resource");
            record.GetRawText().Should().Contain("Sensitive exception message");
            record.GetRawText().Should().NotContain("sensitive-project");

            var prepared = await client.CallToolAsync("prepare-error-report", arguments, cancellationToken: cancellation.Token);
            prepared.IsError.Should().NotBeTrue();
            var payload = prepared.StructuredContent.GetValueOrDefault().GetProperty("data").GetProperty("payloadJson").GetString();
            payload.Should().Contain("resources/read");
            payload.Should().NotContain("sensitive-project");
            dispatcher.Verify(item => item.DispatchAsync(It.IsAny<PreparedDispatchPayload>(), It.IsAny<ExceptionMessageHandling>(), It.IsAny<CancellationToken>()), Times.Never);

            var unknown = async () => await client.ReadResourceAsync(new Uri("probe://unknown/resource"), cancellationToken: cancellation.Token);
            var missing = await unknown.Should().ThrowAsync<McpProtocolException>();
            missing.Which.ErrorCode.Should().Be(McpErrorCode.ResourceNotFound);
            missing.Which.Data.Contains("diagnostics").Should().BeFalse();

            var healthy = await client.ReadResourceAsync(new Uri("skill://roslyn-workbench/SKILL.md"), cancellationToken: cancellation.Token);
            healthy.Contents.Should().ContainSingle();
        }
        finally
        {
            await cancellation.CancelAsync();
            await clientToServer.Writer.CompleteAsync();
            await serverToClient.Writer.CompleteAsync();
            await serverTask;
        }
    }
}
