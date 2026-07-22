using System.IO.Pipelines;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Client;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class McpCancellationProtocolIntegrationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ActiveToolRequest_WHEN_ClientSendsCancellationNotification_THEN_ShouldCancelServerHandlerToken()
    {
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationNotificationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = McpServerTool.Create(
            async (CancellationToken cancellationToken) =>
            {
                handlerStarted.TrySetResult();
                try
                {
                    await handlerRelease.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    handlerCancellationObserved.TrySetResult();
                    throw;
                }

                return "Completed";
            },
            new McpServerToolCreateOptions
            {
                Name = "cancellation-probe",
            });

        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddMcpServer()
            .WithTools([tool])
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream());

        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        var server = serviceProvider.GetRequiredService<McpServer>();
        await using var cancellationRegistration = server.RegisterNotificationHandler(
            NotificationMethods.CancelledNotification,
            (_, _) =>
            {
                cancellationNotificationObserved.TrySetResult();
                return ValueTask.CompletedTask;
            });

        using var serverCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = server.RunAsync(serverCancellation.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServerPipe.Writer.AsStream(),
                serverToClientPipe.Reader.AsStream(),
                NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);

        using var invocationCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeoutCancellation.CancelAfter(_timeout);

        var requestId = new RequestId("cancellation-probe-request");
        var invocation = client.SendRequestAsync<CallToolRequestParams, CallToolResult>(
            RequestMethods.ToolsCall,
            new CallToolRequestParams
            {
                Name = "cancellation-probe",
            },
            requestId: requestId,
            cancellationToken: invocationCancellation.Token).AsTask();

        await handlerStarted.Task.WaitAsync(timeoutCancellation.Token);

        await client.SendNotificationAsync(
            NotificationMethods.CancelledNotification,
            new CancelledNotificationParams
            {
                RequestId = requestId,
            },
            cancellationToken: timeoutCancellation.Token);

        await cancellationNotificationObserved.Task.WaitAsync(timeoutCancellation.Token);
        await handlerCancellationObserved.Task.WaitAsync(timeoutCancellation.Token);

        await invocationCancellation.CancelAsync();
        var action = async () => await invocation;
        await action.Should().ThrowAsync<OperationCanceledException>();

        await serverCancellation.CancelAsync();
        await clientToServerPipe.Writer.CompleteAsync();
        await serverToClientPipe.Writer.CompleteAsync();
        await serverTask;
    }
}
