using System.IO.Pipelines;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using ModelContextProtocol;
using ModelContextProtocol.Client;

using Roslyn.Workbench.Mcp.ErrorReporting.Capture;
using Roslyn.Workbench.Mcp.ToolExecution;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class PluginMcpRequestHandlerProtocolIntegrationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

#pragma warning disable MCPEXP001

    [Theory]
    [InlineData(false, "missing-tool", "is not registered")]
    [InlineData(true, "plugin-tool", "does not support task-augmented execution")]
    [Trait("Category", "Integration")]
    public async Task GIVEN_InvalidPluginCall_WHEN_RoutedThroughInstalledFilter_THEN_ShouldReturnProtocolErrorWithoutCapturingFailure(
        bool taskAugmented,
        string requestedToolName,
        string expectedMessage)
    {
        var capturedErrorStore = new Mock<ICapturedErrorStore>();
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(capturedErrorStore.Object);

        await using var workbenchServices = builder.Services.BuildServiceProvider();
        var installedFilter = workbenchServices
            .GetRequiredService<IOptions<McpServerOptions>>()
            .Value
            .Filters
            .Request
            .CallToolFilters
            .Single();
        var filter = workbenchServices.GetRequiredService<UnhandledToolExceptionFilter>();
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        var protocolTool = new Tool
        {
            Name = "plugin-tool",
        };
        var tool = new Mock<McpServerTool>();
        tool.SetupGet(static value => value.ProtocolTool).Returns(protocolTool);
        var healthProtocolTool = new Tool
        {
            Name = "health-probe",
        };
        var healthTool = new Mock<McpServerTool>();
        healthTool.SetupGet(static value => value.ProtocolTool).Returns(healthProtocolTool);
        healthTool
            .Setup(value => value.InvokeAsync(
                It.IsAny<RequestContext<CallToolRequestParams>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallToolResult
            {
                Content = [],
                IsError = false,
            });
        using var catalogState = new PluginCatalogState();
        var tools = new Dictionary<string, McpServerTool>(StringComparer.Ordinal)
        {
            [protocolTool.Name] = tool.Object,
            [healthProtocolTool.Name] = healthTool.Object,
        };
        var runtimeCatalog = new PluginRuntimeCatalogSnapshot
        {
            Tools = tools,
        };
        catalogState.Publish(runtimeCatalog);
        var pluginHandler = new PluginMcpRequestHandler(catalogState);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(filter);
        services
            .AddMcpServer(options => options.Handlers.CallToolHandler = pluginHandler.CallToolAsync)
            .WithRequestFilters(requestFilters => requestFilters.AddCallToolFilter(installedFilter))
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream());

        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        var server = serviceProvider.GetRequiredService<McpServer>();
        using var serverCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = server.RunAsync(serverCancellation.Token);
        var clientTransport = new StreamClientTransport(
            clientToServerPipe.Writer.AsStream(),
            serverToClientPipe.Reader.AsStream(),
            NullLoggerFactory.Instance);
        await using var client = await McpClient.CreateAsync(
            clientTransport,
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeoutCancellation.CancelAfter(_timeout);

        try
        {
            var request = new CallToolRequestParams
            {
                Name = requestedToolName,
                Task = taskAugmented ? new McpTaskMetadata() : null,
            };

            var action = async () => await client.SendRequestAsync<CallToolRequestParams, CallToolResult>(
                RequestMethods.ToolsCall,
                request,
                cancellationToken: timeoutCancellation.Token);

            var exception = await action.Should().ThrowAsync<McpProtocolException>();
            exception.Which.ErrorCode.Should().Be(McpErrorCode.InvalidParams);
            exception.Which.Message.Should().Contain(expectedMessage);
            tool.Verify(
                value => value.InvokeAsync(It.IsAny<RequestContext<CallToolRequestParams>>(), It.IsAny<CancellationToken>()),
                Times.Never);
            capturedErrorStore.Verify(item => item.Add(It.IsAny<CapturedErrorRecord>()), Times.Never);

            var healthResult = await client.CallToolAsync(
                healthProtocolTool.Name,
                cancellationToken: timeoutCancellation.Token);

            healthResult.IsError.Should().NotBeTrue();
            healthTool.Verify(
                value => value.InvokeAsync(It.IsAny<RequestContext<CallToolRequestParams>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            await serverCancellation.CancelAsync();
            await clientToServerPipe.Writer.CompleteAsync();
            await serverToClientPipe.Writer.CompleteAsync();
            await serverTask;
        }
    }

#pragma warning restore MCPEXP001
}
